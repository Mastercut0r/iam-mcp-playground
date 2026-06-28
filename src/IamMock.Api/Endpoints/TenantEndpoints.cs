using IamMock.Api.Data;

namespace IamMock.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants").WithTags("Tenants");

        group.MapGet("/", (MockDataStore db) => db.GetTenants())
            .WithSummary("List all tenants.");

        group.MapGet("/{id:guid}", (Guid id, MockDataStore db) =>
                db.GetTenant(id) is { } tenant ? Results.Ok(tenant) : Results.NotFound())
            .WithSummary("Get a single tenant by id.");

        group.MapGet("/{id:guid}/users", (Guid id, MockDataStore db) =>
                db.GetTenant(id) is null ? Results.NotFound() : Results.Ok(db.GetUsers(id)))
            .WithSummary("List the users belonging to a tenant.");

        group.MapGet("/{id:guid}/licenses", (Guid id, MockDataStore db) =>
                db.GetTenant(id) is null ? Results.NotFound() : Results.Ok(db.GetLicenses(id)))
            .WithSummary("List the licenses owned by a tenant.");

        return app;
    }
}
