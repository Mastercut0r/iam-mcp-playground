namespace IamMock.Contracts.Requests;

/// <summary>Payload for creating a new user.</summary>
public sealed record CreateUserRequest
{
    /// <summary>Tenant the user will belong to. Required.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Login / contact email. Required, unique within the tenant.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Full display name. Required.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Optional department; defaults to "Unassigned".</summary>
    public string? Department { get; init; }

    /// <summary>Optional roles to assign at creation time.</summary>
    public IReadOnlyList<Guid>? RoleIds { get; init; }

    /// <summary>Optional licenses to assign at creation time (must have free seats).</summary>
    public IReadOnlyList<Guid>? LicenseIds { get; init; }
}
