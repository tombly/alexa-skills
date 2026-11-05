namespace AlexaSkills.Alexa;

public sealed class AlexaOptions
{
    /// <summary>
    /// Check each request's signature and timestamp. Can only be switched off for local development.
    /// </summary>
    public bool VerifyRequests { get; set; } = true;

    /// <summary>
    /// The skill ID (amzn1.ask.skill.…) each endpoint accepts, keyed by <see cref="Skill.Name"/>.
    /// </summary>
    public Dictionary<string, string> SkillIds { get; set; } = [];
}
