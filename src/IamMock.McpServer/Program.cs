using IamMock.McpServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdio transport uses stdout for the protocol, so all logs must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Typed client over the IAM Mock REST API. Override the base address via
// configuration key "IamApi:BaseUrl" or environment variable "IamApi__BaseUrl".
var apiBaseUrl = builder.Configuration["IamApi:BaseUrl"] ?? "http://localhost:5080";
builder.Services.AddHttpClient<IamApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
