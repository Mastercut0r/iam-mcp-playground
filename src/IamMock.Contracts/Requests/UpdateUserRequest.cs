namespace IamMock.Contracts.Requests;

/// <summary>Partial update for a user. Only non-null fields are applied.</summary>
public sealed record UpdateUserRequest
{
    /// <summary>New display name, if provided.</summary>
    public string? DisplayName { get; init; }

    /// <summary>New department, if provided.</summary>
    public string? Department { get; init; }

    /// <summary>Enable/disable the account, if provided.</summary>
    public bool? IsActive { get; init; }
}
