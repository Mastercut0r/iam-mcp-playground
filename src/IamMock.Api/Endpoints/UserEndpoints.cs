using IamMock.Api.Data;
using IamMock.Contracts.Models;
using IamMock.Contracts.Requests;

namespace IamMock.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users");

        // ---- Reads ---------------------------------------------------------

        group.MapGet("/", (MockDataStore db, Guid? tenantId, string? search) =>
            {
                IEnumerable<User> users = db.GetUsers(tenantId);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    users = users.Where(u =>
                        u.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                return users.ToList();
            })
            .WithSummary("List users, optionally filtered by tenant and/or a free-text search.");

        group.MapGet("/{id:guid}", (Guid id, MockDataStore db) =>
                db.GetUser(id) is { } user ? Results.Ok(user) : Results.NotFound())
            .WithSummary("Get a single user by id.");

        // ---- Writes --------------------------------------------------------

        group.MapPost("/", (CreateUserRequest request, MockDataStore db) =>
            {
                var user = db.CreateUser(request);
                return Results.Created($"/users/{user.Id}", user);
            })
            .WithSummary("Create a new user.");

        group.MapPatch("/{id:guid}", (Guid id, UpdateUserRequest request, MockDataStore db) =>
                Results.Ok(db.UpdateUser(id, request)))
            .WithSummary("Update a user's display name, department or active state.");

        group.MapDelete("/{id:guid}", (Guid id, MockDataStore db) =>
            {
                db.DeleteUser(id);
                return Results.NoContent();
            })
            .WithSummary("Delete a user.");

        group.MapPost("/{id:guid}/roles/{roleId:guid}", (Guid id, Guid roleId, MockDataStore db) =>
                Results.Ok(db.AssignRole(id, roleId)))
            .WithSummary("Assign a role to a user.");

        group.MapDelete("/{id:guid}/roles/{roleId:guid}", (Guid id, Guid roleId, MockDataStore db) =>
                Results.Ok(db.UnassignRole(id, roleId)))
            .WithSummary("Remove a role from a user.");

        group.MapPost("/{id:guid}/licenses/{licenseId:guid}", (Guid id, Guid licenseId, MockDataStore db) =>
                Results.Ok(db.AssignLicense(id, licenseId)))
            .WithSummary("Assign a license seat to a user.");

        group.MapDelete("/{id:guid}/licenses/{licenseId:guid}", (Guid id, Guid licenseId, MockDataStore db) =>
                Results.Ok(db.RevokeLicense(id, licenseId)))
            .WithSummary("Revoke a license seat from a user.");

        return app;
    }
}
