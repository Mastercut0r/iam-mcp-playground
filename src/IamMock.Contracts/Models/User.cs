namespace IamMock.Contracts.Models;

/// <summary>
/// An identity within a tenant, with assigned roles and licenses.
/// </summary>
public sealed class User
{
    /// <summary>Unique identifier of the user.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant the user belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Login / contact email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Full display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Department the user works in.</summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>Whether the account is enabled.</summary>
    public bool IsActive { get; set; }

    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Ids of roles assigned to the user.</summary>
    public IReadOnlyList<Guid> RoleIds { get; set; } = [];

    /// <summary>Ids of licenses assigned to the user.</summary>
    public IReadOnlyList<Guid> LicenseIds { get; set; } = [];
}
