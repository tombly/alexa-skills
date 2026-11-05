namespace AlexaSkills.Alexa;

// The parts of Alexa's request envelope these skills use:
// https://developer.amazon.com/docs/alexa/custom-skills/request-and-response-json-reference.html

public sealed record SkillRequest(Session? Session, RequestContext? Context, Request Request)
{
    public string? ApplicationId => Context?.System?.Application?.ApplicationId ?? Session?.Application?.ApplicationId;

    public string? UserId => Context?.System?.User?.UserId ?? Session?.User?.UserId;

    /// <summary>
    /// True when this request opened the session, e.g. a one-shot "Alexa, ask time clock to…".
    /// </summary>
    public bool IsNewSession => Session?.New ?? true;
}

public sealed record Session(bool New, Application? Application, User? User);

public sealed record Application(string ApplicationId);

public sealed record User(string UserId);

public sealed record RequestContext(SystemState? System);

public sealed record SystemState(Application? Application, User? User);

/// <param name="Reason">Why a SessionEndedRequest ended the session: USER_INITIATED, ERROR or EXCEEDED_MAX_REPROMPTS.</param>
/// <param name="Error">What went wrong, on a SessionEndedRequest with reason ERROR or a System.ExceptionEncountered.</param>
public sealed record Request(string Type, DateTimeOffset Timestamp, Intent? Intent, string? Reason = null, SkillError? Error = null);

/// <param name="Type">e.g. INVALID_RESPONSE, ENDPOINT_TIMEOUT or INTERNAL_SERVICE_ERROR.</param>
public sealed record SkillError(string? Type, string? Message);

/// <param name="ConfirmationStatus">NONE, or CONFIRMED or DENIED once the user has answered a confirmation prompt.</param>
public sealed record Intent(string Name, IReadOnlyDictionary<string, Slot>? Slots, string? ConfirmationStatus = null)
{
    public string? SlotValue(string name) => Slots?.GetValueOrDefault(name)?.Value;

    public string? ResolvedSlotValue(string name) => Slots?.GetValueOrDefault(name)?.ResolvedValue;
}

public sealed record Slot(string? Value, SlotResolutions? Resolutions)
{
    /// <summary>
    /// The slot type's own value when Alexa matched what it heard to one (e.g. a synonym), otherwise what it heard.
    /// </summary>
    public string? ResolvedValue => Resolutions?.ResolutionsPerAuthority?
        .Where(authority => authority.Status?.Code == "ER_SUCCESS_MATCH")
        .SelectMany(authority => authority.Values ?? [])
        .Select(match => match.Value?.Name)
        .FirstOrDefault(name => name is not null) ?? Value;
}

public sealed record SlotResolutions(IReadOnlyList<ResolutionAuthority>? ResolutionsPerAuthority);

public sealed record ResolutionAuthority(ResolutionStatus? Status, IReadOnlyList<ResolutionMatch>? Values);

public sealed record ResolutionStatus(string Code);

public sealed record ResolutionMatch(ResolutionValue? Value);

public sealed record ResolutionValue(string Name);
