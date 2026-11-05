using AlexaSkills.Skills.TimeClock;

namespace AlexaSkills.Tests.TestSupport;

/// <summary>
/// Keeps each user's time card as JSON, like the table does, so tests exercise the stored format.
/// </summary>
sealed class InMemoryTimeCardStore : ITimeCardStore
{
    public Dictionary<string, string> Rows { get; } = [];

    public TimeCard Load(string userId) => TimeCard.FromJson(Rows.GetValueOrDefault(userId));

    public Task<TimeCard> LoadAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Load(userId));

    public Task SaveAsync(string userId, TimeCard timeCard, CancellationToken cancellationToken)
    {
        Rows[userId] = timeCard.ToJson();
        return Task.CompletedTask;
    }
}
