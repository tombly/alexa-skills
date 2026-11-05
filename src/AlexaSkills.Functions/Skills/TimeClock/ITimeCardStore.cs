namespace AlexaSkills.Skills.TimeClock;

public interface ITimeCardStore
{
    /// <summary>
    /// Returns the user's time card, or an empty one if they haven't used the skill yet.
    /// </summary>
    Task<TimeCard> LoadAsync(string userId, CancellationToken cancellationToken);

    Task SaveAsync(string userId, TimeCard timeCard, CancellationToken cancellationToken);
}
