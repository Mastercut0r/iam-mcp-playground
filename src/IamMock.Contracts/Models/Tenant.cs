namespace IamMock.Contracts.Models;

/// <summary>
/// A customer organization. Users, licenses and assignments are scoped to a tenant.
/// </summary>
public sealed class Tenant
{
    /// <summary>Unique identifier of the tenant.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the organization, e.g. "Contoso".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Primary DNS domain, e.g. "contoso.com".</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Whether the tenant is currently active.</summary>
    public bool IsActive { get; set; }

    /// <summary>When the tenant was provisioned.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
