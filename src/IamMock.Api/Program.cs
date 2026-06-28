using IamMock.Api.Data;
using IamMock.Api.Endpoints;
using Microsoft.AspNetCore.Diagnostics;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Pin a predictable port for the mock unless the host explicitly overrides it
// (e.g. via the ASPNETCORE_URLS environment variable). The MCP server defaults
// to this same address.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls("http://localhost:5080");

// The mock data lives in a single, in-memory singleton.
builder.Services.AddSingleton<MockDataStore>();

// OpenAPI document (served at /openapi/v1.json).
builder.Services.AddOpenApi();

var app = builder.Build();

// Eagerly build the data so generation cost / errors happen at startup, not on first request.
app.Services.GetRequiredService<MockDataStore>();

// Map domain rule violations to proper HTTP status codes.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (error is DomainException domainError)
    {
        context.Response.StatusCode = domainError.Type switch
        {
            DomainErrorType.NotFound => StatusCodes.Status404NotFound,
            DomainErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        await context.Response.WriteAsJsonAsync(new { error = domainError.Message });
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    }
}));

app.MapOpenApi();

// Interactive API reference UI at /scalar.
app.MapScalarApiReference(options => options.WithTitle("IAM Mock API"));

app.MapGet("/", () => Results.Redirect("/scalar"))
    .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("System")
    .WithSummary("Liveness probe.");

app.MapTenantEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();
app.MapLicenseEndpoints();

app.Run();
