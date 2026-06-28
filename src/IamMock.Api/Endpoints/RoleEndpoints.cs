using IamMock.Api.Data;

namespace IamMock.Api.Endpoints;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/roles").WithTags("Roles");

        group.MapGet("/", (MockDataStore db) => db.GetRoles())
            .WithSummary("List all roles and their permissions.");

        group.MapGet("/{id:guid}", (Guid id, MockDataStore db) =>
                db.GetRole(id) is { } role ? Results.Ok(role) : Results.NotFound())
            .WithSummary("Get a single role by id.");

        return app;
    }
}
