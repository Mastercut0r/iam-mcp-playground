using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IamMock.Contracts.Models;
using IamMock.Contracts.Requests;
using ModelContextProtocol;

namespace IamMock.McpServer;

/// <summary>
/// Typed HTTP client over the IAM Mock REST API. Base address is configured via DI.
/// </summary>
public sealed class IamApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<Tenant>> GetTenantsAsync(CancellationToken ct = default) =>
        await GetListAsync<Tenant>("tenants", ct);

    public Task<Tenant?> GetTenantAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<Tenant>($"tenants/{id}", ct);

    public async Task<IReadOnlyList<User>> GetUsersAsync(Guid? tenantId, string? search, CancellationToken ct = default)
    {
        var query = BuildQuery(("tenantId", tenantId?.ToString()), ("search", search));
        return await GetListAsync<User>($"users{query}", ct);
    }

    public Task<User?> GetUserAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<User>($"users/{id}", ct);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken ct = default) =>
        await GetListAsync<Role>("roles", ct);

    public Task<Role?> GetRoleAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<Role>($"roles/{id}", ct);

    public async Task<IReadOnlyList<License>> GetLicensesAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var query = BuildQuery(("tenantId", tenantId?.ToString()));
        return await GetListAsync<License>($"licenses{query}", ct);
    }

    public Task<License?> GetLicenseAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<License>($"licenses/{id}", ct);

    // ---- Writes ------------------------------------------------------------

    public async Task<User> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync("users", request, JsonOptions, ct));
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<User>(JsonOptions, ct))!;
    }

    public async Task<User> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.PatchAsJsonAsync($"users/{id}", request, JsonOptions, ct));
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<User>(JsonOptions, ct))!;
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.DeleteAsync($"users/{id}", ct));
        await EnsureSuccessAsync(response, ct);
    }

    public Task<User> AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
        PostForUserAsync($"users/{userId}/roles/{roleId}", ct);

    public Task<User> UnassignRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
        DeleteForUserAsync($"users/{userId}/roles/{roleId}", ct);

    public Task<User> AssignLicenseAsync(Guid userId, Guid licenseId, CancellationToken ct = default) =>
        PostForUserAsync($"users/{userId}/licenses/{licenseId}", ct);

    public Task<User> RevokeLicenseAsync(Guid userId, Guid licenseId, CancellationToken ct = default) =>
        DeleteForUserAsync($"users/{userId}/licenses/{licenseId}", ct);

    // ---- helpers -----------------------------------------------------------

    private async Task<User> PostForUserAsync(string path, CancellationToken ct)
    {
        using var response = await SendAsync(() => http.PostAsync(path, content: null, ct));
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<User>(JsonOptions, ct))!;
    }

    private async Task<User> DeleteForUserAsync(string path, CancellationToken ct)
    {
        using var response = await SendAsync(() => http.DeleteAsync(path, ct));
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<User>(JsonOptions, ct))!;
    }

    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? message = null;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions, ct);
            message = body?.Error;
        }
        catch
        {
            // body was not the expected { "error": ... } shape; fall back to the status code
        }

        throw new McpException(
            message ?? $"API request failed with {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    private sealed record ErrorBody(string? Error);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<List<T>>(path, JsonOptions, ct) ?? [];
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(ex);
        }
    }

    private async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(ex);
        }
    }

    private McpException Unreachable(Exception inner) =>
        new($"Could not reach the IAM Mock API at '{http.BaseAddress}'. Is it running? " +
            "Start it with: dotnet run --project src/IamMock.Api", inner);

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var pairs = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}")
            .ToArray();

        return pairs.Length == 0 ? string.Empty : "?" + string.Join("&", pairs);
    }
}
