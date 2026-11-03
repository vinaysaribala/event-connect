namespace EventConnectService;

public enum ServiceResultStatus
{
    Success,
    Invalid,
    Conflict,
    Unauthorized,
    Forbidden,
    NotFound
}

/// <summary>Outcome of a service call that can fail in a way the caller should report (validation, conflict).</summary>
/// <param name="Message">Title of the error response: the conflict or forbidden reason, or an optional title for a validation failure.</param>
public record ServiceResult(ServiceResultStatus Status, IReadOnlyDictionary<string, string[]> Errors, string? Message = null)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public bool Succeeded => Status == ServiceResultStatus.Success;

    public static ServiceResult Success() => new(ServiceResultStatus.Success, NoErrors);

    public static ServiceResult Invalid(string field, string message) =>
        new(ServiceResultStatus.Invalid, new Dictionary<string, string[]> { [field] = [message] });

    public static ServiceResult Invalid(IReadOnlyDictionary<string, string[]> errors, string? message = null) =>
        new(ServiceResultStatus.Invalid, errors, message);

    public static ServiceResult Conflict(string message) => new(ServiceResultStatus.Conflict, NoErrors, message);

    public static ServiceResult Unauthorized() => new(ServiceResultStatus.Unauthorized, NoErrors);

    public static ServiceResult Forbidden(string message) => new(ServiceResultStatus.Forbidden, NoErrors, message);

    public static ServiceResult NotFound() => new(ServiceResultStatus.NotFound, NoErrors);
}

/// <summary>A <see cref="ServiceResult"/> carrying a value on success.</summary>
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, ServiceResult outcome)
    {
        Value = value;
        Outcome = outcome;
    }

    public T? Value { get; }
    public ServiceResult Outcome { get; }
    public bool Succeeded => Outcome.Succeeded;

    public static implicit operator ServiceResult<T>(T value) => new(value, ServiceResult.Success());
    public static implicit operator ServiceResult<T>(ServiceResult failure) => new(default, failure);
}
