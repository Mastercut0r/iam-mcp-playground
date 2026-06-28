using System.ComponentModel;
using IamMock.Contracts.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IamMock.McpServer.Tools;

[McpServerToolType]
public static class TenantTools
{
    [McpServerTool(Name = "list_tenants")]
    [Description("List all tenants (customer organizations) in the IAM system.")]
    public static Task<IReadOnlyList<Tenant>> ListTenants(
        IamApiClient api,
        CancellationToken ct = default)
        => api.GetTenantsAsync(ct);

    [McpServerTool(Name = "get_tenant")]
    [Description("Get a single tenant by its id.")]
    public static async Task<Tenant> GetTenant(
        IamApiClient api,
        [Description("The tenant's unique id (GUID).")] Guid id,
        CancellationToken ct = default)
        => await api.GetTenantAsync(id, ct)
           ?? throw new McpException($"Tenant '{id}' was not found.");
}
