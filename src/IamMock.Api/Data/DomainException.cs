namespace IamMock.Api.Data;

/// <summary>Classifies a <see cref="DomainException"/> so it can be mapped to an HTTP status.</summary>
public enum DomainErrorType
{
    Validation,
    NotFound,
    Conflict,
}

/// <summary>
/// A business-rule violation in the mock store. Mapped to 400/404/409 by the API's
/// exception handler.
/// </summary>
public sealed class DomainException(DomainErrorType type, string message) : Exception(message)
{
    public DomainErrorType Type { get; } = type;

    public static DomainException Validation(string message) => new(DomainErrorType.Validation, message);
    public static DomainException NotFound(string message) => new(DomainErrorType.NotFound, message);
    public static DomainException Conflict(string message) => new(DomainErrorType.Conflict, message);
}
