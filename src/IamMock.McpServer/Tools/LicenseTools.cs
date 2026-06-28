using System.ComponentModel;
using IamMock.Contracts.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

// Disambiguate from System.ComponentModel.License (pulled in by the using above).
using License = IamMock.Contracts.Models.License;

namespace IamMock.McpServer.Tools;

[McpServerToolType]
public static class LicenseTools
{
    [McpServerTool(Name = "list_licenses")]
    [Description("List license seat pools, optionally filtered by tenant.")]
    public static Task<IReadOnlyList<License>> ListLicenses(
        IamApiClient api,
        [Description("Optional tenant id (GUID) to only return that tenant's licenses.")] Guid? tenantId = null,
        CancellationToken ct = default)
        => api.GetLicensesAsync(tenantId, ct);

    [McpServerTool(Name = "get_license")]
    [Description("Get a single license by id.")]
    public static async Task<License> GetLicense(
        IamApiClient api,
        [Description("The license's unique id (GUID).")] Guid id,
        CancellationToken ct = default)
        => await api.GetLicenseAsync(id, ct)
           ?? throw new McpException($"License '{id}' was not found.");

    [McpServerTool(Name = "license_summary")]
    [Description("Summarize license seat usage for a tenant: total, assigned and available seats per SKU.")]
    public static async Task<IReadOnlyList<object>> LicenseSummary(
        IamApiClient api,
        [Description("The tenant id (GUID) to summarize licenses for.")] Guid tenantId,
        CancellationToken ct = default)
    {
        var licenses = await api.GetLicensesAsync(tenantId, ct);
        return licenses.Select(l => (object)new
        {
            l.SkuName,
            l.TotalSeats,
            l.AssignedSeats,
            l.AvailableSeats,
            l.ExpiresAt,
        }).ToList();
    }
}
