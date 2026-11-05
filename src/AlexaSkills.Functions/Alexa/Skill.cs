namespace AlexaSkills.Alexa;

public abstract class Skill
{
    /// <summary>
    /// Identifies the skill in settings, e.g. Alexa:SkillIds:{Name}.
    /// </summary>
    public abstract string Name { get; }

    public Task<SkillResponse> HandleAsync(SkillRequest request, CancellationToken cancellationToken) => request.Request switch
    {
        { Type: "LaunchRequest" } => OnLaunchAsync(request, cancellationToken),
        { Type: "IntentRequest", Intent: { } intent } => OnIntentAsync(request, intent, cancellationToken),
        // SessionEndedRequest, System.ExceptionEncountered, and anything else.
        _ => Task.FromResult(Reply.Empty()),
    };

    protected abstract Task<SkillResponse> OnLaunchAsync(SkillRequest request, CancellationToken cancellationToken);

    protected abstract Task<SkillResponse> OnIntentAsync(SkillRequest request, Intent intent, CancellationToken cancellationToken);
}
