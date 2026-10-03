namespace SeniorConnect.SharedKernel;

/// <summary>
/// Domain and application operations return a Result rather than throwing.
/// Exceptions are for bugs; a rejected business rule is not a bug.
/// </summary>
public readonly record struct Result
{
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public readonly record struct Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

/// <summary>
/// The <see cref="Code"/> is the stable contract with the client.
/// Flutter switches on Code, never on Title or Detail — those are localised
/// and may change. See docs/api/api-conventions.md.
/// </summary>
public sealed record Error(
    string Code,
    string Detail,
    ErrorKind Kind,
    IReadOnlyDictionary<string, object>? Extensions = null)
{
    // --- Authorization -----------------------------------------------------
    public static Error CapabilityMissing(string capability) => new(
        "CAPABILITY_MISSING",
        $"The capability '{capability}' is required.",
        ErrorKind.Forbidden,
        new Dictionary<string, object> { ["capability"] = capability });

    /// <summary>
    /// A 403 must always tell the user what is missing and how to fix it.
    /// docs/architecture/authorization.md §5 — this is a product requirement,
    /// not a nicety: an unexplained refusal loses the volunteer.
    /// </summary>
    public static Error TrustLevelInsufficient(
        int required, int current, IReadOnlyList<string> missing) => new(
        "TRUST_LEVEL_INSUFFICIENT",
        "Additional verification is required for this activity.",
        ErrorKind.Forbidden,
        new Dictionary<string, object>
        {
            ["requiredTrustLevel"] = required,
            ["currentTrustLevel"] = current,
            ["missing"] = missing,
        });

    // --- Domain ------------------------------------------------------------
    public static Error InvalidStateTransition(string from, string to) => new(
        "INVALID_STATE_TRANSITION",
        $"Cannot move from '{from}' to '{to}'.",
        ErrorKind.Conflict,
        new Dictionary<string, object> { ["from"] = from, ["to"] = to });

    /// <summary>BR-TRANSPORT-04.</summary>
    public static Error InsuranceUnresolved() => new(
        "INSURANCE_CONTEXT_UNRESOLVED",
        "An activity involving transport in a private vehicle cannot be "
        + "confirmed while the insurance context is unknown.",
        ErrorKind.Conflict);

    /// <summary>BR-SCOPE-02/03 — returns a referral, never a request.</summary>
    public static Error CategoryBlocked(string referralGroup) => new(
        "CATEGORY_BLOCKED",
        "This requires professional care and cannot be arranged here.",
        ErrorKind.Conflict,
        new Dictionary<string, object> { ["referralGroup"] = referralGroup });

    public static Error Validation(string detail) =>
        new("VALIDATION_FAILED", detail, ErrorKind.Validation);

    /// <summary>
    /// Cross-tenant misses use NotFound, never Forbidden. A 403 confirms the
    /// resource exists. docs/architecture/authorization.md §2.
    /// </summary>
    public static Error NotFound(string resource) =>
        new("NOT_FOUND", $"{resource} was not found.", ErrorKind.NotFound);
}

public enum ErrorKind
{
    Validation,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    Concurrency,
}
