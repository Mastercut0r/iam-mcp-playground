namespace IamMock.Contracts.Models;

/// <summary>
/// A pool of seats for a given product SKU, owned by a tenant.
/// </summary>
public sealed class License
{
    /// <summary>Unique identifier of the license.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Product SKU, e.g. "IAM-PRO".</summary>
    public string SkuName { get; set; } = string.Empty;

    /// <summary>Total number of seats purchased.</summary>
    public int TotalSeats { get; set; }

    /// <summary>Number of seats currently assigned to users.</summary>
    public int AssignedSeats { get; set; }

    /// <summary>Seats still available for assignment.</summary>
    public int AvailableSeats => TotalSeats - AssignedSeats;

    /// <summary>When the license expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
