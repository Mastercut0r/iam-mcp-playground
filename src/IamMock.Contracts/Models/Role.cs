namespace IamMock.Contracts.Models;

/// <summary>
/// A role groups a set of permissions. Roles are global and can be assigned to users
/// across tenants (kept deliberately simple for this mock).
/// </summary>
public sealed class Role
{
    /// <summary>Unique identifier of the role.</summary>
    public Guid Id { get; set; }

    /// <summary>Human readable name, e.g. "Tenant Administrator".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short description of what the role is for.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Permission strings granted by this role, e.g. "user.write".</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];
}
