using System.ComponentModel;
using IamMock.Contracts.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IamMock.McpServer.Tools;

[McpServerToolType]
public static class RoleTools
{
    [McpServerTool(Name = "list_roles")]
    [Description("List all roles and the permissions they grant.")]
    public static Task<IReadOnlyList<Role>> ListRoles(
        IamApiClient api,
        CancellationToken ct = default)
        => api.GetRolesAsync(ct);

    [McpServerTool(Name = "get_role")]
    [Description("Get a single role by id, including its permission strings.")]
    public static async Task<Role> GetRole(
        IamApiClient api,
        [Description("The role's unique id (GUID).")] Guid id,
        CancellationToken ct = default)
        => await api.GetRoleAsync(id, ct)
           ?? throw new McpException($"Role '{id}' was not found.");
}
