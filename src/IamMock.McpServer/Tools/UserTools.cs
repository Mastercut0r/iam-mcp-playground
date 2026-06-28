using System.ComponentModel;
using IamMock.Contracts.Models;
using IamMock.Contracts.Requests;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IamMock.McpServer.Tools;

[McpServerToolType]
public static class UserTools
{
    [McpServerTool(Name = "list_users")]
    [Description("List IAM users, optionally filtered by tenant and/or a free-text search over display name and email.")]
    public static Task<IReadOnlyList<User>> ListUsers(
        IamApiClient api,
        [Description("Optional tenant id (GUID) to only return users of that tenant.")] Guid? tenantId = null,
        [Description("Optional case-insensitive search over display name and email.")] string? search = null,
        CancellationToken ct = default)
        => api.GetUsersAsync(tenantId, search, ct);

    [McpServerTool(Name = "get_user")]
    [Description("Get a single user by id, including assigned role ids and license ids.")]
    public static async Task<User> GetUser(
        IamApiClient api,
        [Description("The user's unique id (GUID).")] Guid id,
        CancellationToken ct = default)
        => await api.GetUserAsync(id, ct)
           ?? throw new McpException($"User '{id}' was not found.");

    [McpServerTool(Name = "create_user")]
    [Description("Create a new user in a tenant. Email must be unique within the tenant.")]
    public static Task<User> CreateUser(
        IamApiClient api,
        [Description("Tenant id (GUID) the user belongs to.")] Guid tenantId,
        [Description("Login / contact email, unique within the tenant.")] string email,
        [Description("Full display name.")] string displayName,
        [Description("Optional department; defaults to 'Unassigned'.")] string? department = null,
        CancellationToken ct = default)
        => api.CreateUserAsync(
            new CreateUserRequest
            {
                TenantId = tenantId,
                Email = email,
                DisplayName = displayName,
                Department = department,
            },
            ct);

    [McpServerTool(Name = "update_user")]
    [Description("Update a user's display name, department and/or active state. Omitted fields are left unchanged.")]
    public static Task<User> UpdateUser(
        IamApiClient api,
        [Description("The user's unique id (GUID).")] Guid id,
        [Description("New display name (optional).")] string? displayName = null,
        [Description("New department (optional).")] string? department = null,
        [Description("Enable (true) or disable (false) the account (optional).")] bool? isActive = null,
        CancellationToken ct = default)
        => api.UpdateUserAsync(
            id,
            new UpdateUserRequest
            {
                DisplayName = displayName,
                Department = department,
                IsActive = isActive,
            },
            ct);

    [McpServerTool(Name = "delete_user")]
    [Description("Delete a user by id.")]
    public static async Task<string> DeleteUser(
        IamApiClient api,
        [Description("The user's unique id (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        await api.DeleteUserAsync(id, ct);
        return $"User '{id}' was deleted.";
    }

    [McpServerTool(Name = "assign_role")]
    [Description("Assign a role to a user.")]
    public static Task<User> AssignRole(
        IamApiClient api,
        [Description("The user's unique id (GUID).")] Guid userId,
        [Description("The role's unique id (GUID).")] Guid roleId,
        CancellationToken ct = default)
        => api.AssignRoleAsync(userId, roleId, ct);

    [McpServerTool(Name = "unassign_role")]
    [Description("Remove a role from a user.")]
    public static Task<User> UnassignRole(
        IamApiClient api,
        [Description("The user's unique id (GUID).")] Guid userId,
        [Description("The role's unique id (GUID).")] Guid roleId,
        CancellationToken ct = default)
        => api.UnassignRoleAsync(userId, roleId, ct);

    [McpServerTool(Name = "assign_license")]
    [Description("Assign a license seat to a user. Fails if no seats are available or the license belongs to a different tenant.")]
    public static Task<User> AssignLicense(
        IamApiClient api,
        [Description("The user's unique id (GUID).")] Guid userId,
        [Description("The license's unique id (GUID).")] Guid licenseId,
        CancellationToken ct = default)
        => api.AssignLicenseAsync(userId, licenseId, ct);

    [McpServerTool(Name = "revoke_license")]
    [Description("Revoke a license seat from a user.")]
    public static Task<User> RevokeLicense(
        IamApiClient api,
        [Description("The user's unique id (GUID).")] Guid userId,
        [Description("The license's unique id (GUID).")] Guid licenseId,
        CancellationToken ct = default)
        => api.RevokeLicenseAsync(userId, licenseId, ct);
}
