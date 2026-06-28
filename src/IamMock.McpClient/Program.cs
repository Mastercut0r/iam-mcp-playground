using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// ---------------------------------------------------------------------------
// Resolve how to launch the MCP server and which API it should talk to.
// ---------------------------------------------------------------------------
var serverDll = ResolveServerDll();
if (!File.Exists(serverDll))
{
    Console.Error.WriteLine($"MCP server not found at:\n  {serverDll}\n");
    Console.Error.WriteLine("Build the solution first:  dotnet build");
    Console.Error.WriteLine("Or set IAM_MCP_SERVER_DLL to the server dll path.");
    return 1;
}

var apiBaseUrl = Environment.GetEnvironmentVariable("IAM_API_BASEURL") ?? "http://localhost:5080";

Console.WriteLine($"Launching MCP server: {serverDll}");
Console.WriteLine($"Server -> IAM API:    {apiBaseUrl}\n");

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "IamMock MCP Server",
    Command = "dotnet",
    Arguments = [serverDll],
    EnvironmentVariables = new Dictionary<string, string?>
    {
        ["IamApi__BaseUrl"] = apiBaseUrl,
    },
});

await using var client = await McpClient.CreateAsync(transport);

// ---------------------------------------------------------------------------
// Discover the available tools.
// ---------------------------------------------------------------------------
var tools = await client.ListToolsAsync();
Console.WriteLine($"== {tools.Count} tools available ==");
foreach (var tool in tools)
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Scripted demo: walk the IAM graph from tenants -> users -> licenses.
// ---------------------------------------------------------------------------
Console.WriteLine("== Demo ==");

Console.WriteLine("\n# list_tenants");
var tenantsResult = await CallAsync(client, "list_tenants", null);
PrintResult(tenantsResult);

var firstTenantId = ExtractId(GetText(tenantsResult));
if (firstTenantId is { } tenantId)
{
    Console.WriteLine($"\n# list_users (tenantId = {tenantId})");
    PrintResult(await CallAsync(client, "list_users", new() { ["tenantId"] = tenantId.ToString() }));

    Console.WriteLine($"\n# license_summary (tenantId = {tenantId})");
    PrintResult(await CallAsync(client, "license_summary", new() { ["tenantId"] = tenantId.ToString() }));

    await RunWriteDemoAsync(client, tenantId);
}

Console.WriteLine("\n# list_roles");
PrintResult(await CallAsync(client, "list_roles", null));

// ---------------------------------------------------------------------------
// Interactive mode: "<tool_name> {json-args}".  Empty line / exit to quit.
// ---------------------------------------------------------------------------
Console.WriteLine("\n== Interactive ==");
Console.WriteLine("Type:  <tool_name> {\"key\": \"value\"}   (empty line or 'exit' to quit)");

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine()?.TrimStart('﻿'); // strip a stray BOM from piped input
    if (string.IsNullOrWhiteSpace(line) || line is "exit" or "quit")
        break;

    var (name, toolArgs) = ParseCommand(line);
    try
    {
        PrintResult(await CallAsync(client, name, toolArgs));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}

return 0;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static ValueTask<CallToolResult> CallAsync(McpClient client, string name, Dictionary<string, object?>? args) =>
    client.CallToolAsync(name, args ?? new Dictionary<string, object?>());

static void PrintResult(CallToolResult result)
{
    var text = GetText(result);
    Console.WriteLine(string.IsNullOrWhiteSpace(text) ? "(no content)" : Prettify(text));
    if (result.IsError == true)
        Console.WriteLine("  ^ tool reported an error");
}

static string GetText(CallToolResult result) =>
    string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

static string Prettify(string maybeJson)
{
    try
    {
        using var doc = JsonDocument.Parse(maybeJson);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
    catch (JsonException)
    {
        return maybeJson;
    }
}

// Extracts an "id" from a tool result, whether it is a single object or an array
// (in which case the first element's id is returned).
static Guid? ExtractId(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() == 0)
                return null;
            element = element[0];
        }

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("id", out var idProp) &&
            idProp.TryGetGuid(out var id))
        {
            return id;
        }
    }
    catch (JsonException)
    {
        // not JSON / unexpected shape
    }

    return null;
}

// Scripted write flow: create a user, give them a license, read them back.
static async Task RunWriteDemoAsync(McpClient client, Guid tenantId)
{
    Console.WriteLine("\n== Write demo ==");
    try
    {
        var email = $"demo.{Guid.NewGuid():N}@example.com";
        Console.WriteLine($"\n# create_user (tenantId = {tenantId}, email = {email})");
        var created = await CallAsync(client, "create_user", new()
        {
            ["tenantId"] = tenantId.ToString(),
            ["email"] = email,
            ["displayName"] = "Demo User",
            ["department"] = "Engineering",
        });
        PrintResult(created);

        var userId = ExtractId(GetText(created));

        var licenses = await CallAsync(client, "list_licenses", new() { ["tenantId"] = tenantId.ToString() });
        var licenseId = ExtractId(GetText(licenses));

        if (userId is { } uid && licenseId is { } lid)
        {
            Console.WriteLine($"\n# assign_license (userId = {uid}, licenseId = {lid})");
            PrintResult(await CallAsync(client, "assign_license", new()
            {
                ["userId"] = uid.ToString(),
                ["licenseId"] = lid.ToString(),
            }));

            Console.WriteLine($"\n# get_user ({uid})  <- licenseIds now contains the assigned license");
            PrintResult(await CallAsync(client, "get_user", new() { ["id"] = uid.ToString() }));
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Write demo error: {ex.Message}");
    }
}

static (string Name, Dictionary<string, object?> Args) ParseCommand(string line)
{
    line = line.Trim();
    var space = line.IndexOf(' ');
    if (space < 0)
        return (line, new Dictionary<string, object?>());

    var name = line[..space];
    var argsJson = line[(space + 1)..].Trim();
    var args = new Dictionary<string, object?>();

    if (!string.IsNullOrWhiteSpace(argsJson))
    {
        using var doc = JsonDocument.Parse(argsJson);
        foreach (var prop in doc.RootElement.EnumerateObject())
            args[prop.Name] = ToClrValue(prop.Value);
    }

    return (name, args);
}

static object? ToClrValue(JsonElement element) => element.ValueKind switch
{
    JsonValueKind.String => element.GetString(),
    JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    JsonValueKind.Null => null,
    _ => element.GetRawText(),
};

static string ResolveServerDll()
{
    var overridePath = Environment.GetEnvironmentVariable("IAM_MCP_SERVER_DLL");
    if (!string.IsNullOrWhiteSpace(overridePath))
        return overridePath;

    const string config =
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    // Walk up from the client's output folder until we find the repo's "src" directory.
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        dir = dir.Parent;

    var root = dir?.FullName ?? AppContext.BaseDirectory;
    return Path.Combine(root, "src", "IamMock.McpServer", "bin", config, "net10.0", "IamMock.McpServer.dll");
}
