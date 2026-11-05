using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlexaSkills.Skills.TimeClock;

/// <summary>
/// One user's running total of minutes for each task, saved as JSON:
/// {"tasks": [...], "totals": {"meetings": 90, ...}}.
/// </summary>
public sealed class TimeCard
{
    public static readonly IReadOnlyList<string> TaskNames =
    [
        "accounting",
        "administrative",
        "break",
        "cleaning",
        "customer service",
        "data entry",
        "design",
        "email",
        "errands",
        "exercise",
        "filing",
        "lunch",
        "mail",
        "marketing",
        "meetings",
        "organizing",
        "phone calls",
        "planning",
        "programming",
        "research",
        "reviewing",
        "sales",
        "shipping",
        "training",
        "travel",
        "typing",
        "writing",
        "work",
    ];

    private readonly List<string> _tasks;
    // long, because a couple of saved rows hold totals too big for an int.
    private readonly Dictionary<string, long> _minutes;

    private TimeCard(List<string> tasks, Dictionary<string, long> minutes)
    {
        _tasks = tasks;
        _minutes = minutes;
    }

    public static TimeCard FromJson(string? json)
    {
        var stored = json is null ? null : JsonSerializer.Deserialize<StoredTimeCard>(json);
        var tasks = new List<string>();
        var minutes = new Dictionary<string, long>();

        // A row written before a task joined the list won't have that task yet, so add any that are missing.
        foreach (var task in (stored?.Tasks ?? []).Concat(TaskNames))
        {
            if (minutes.TryAdd(task, stored?.Totals?.GetValueOrDefault(task) ?? 0))
            {
                tasks.Add(task);
            }
        }
        return new TimeCard(tasks, minutes);
    }

    public string ToJson() => JsonSerializer.Serialize(new StoredTimeCard(_tasks, _minutes));

    /// <summary>
    /// Finds the task Alexa heard, ignoring case and stray spaces.
    /// </summary>
    public string? FindTask(string? spoken) =>
        spoken is null ? null : _tasks.Find(task => string.Equals(task, spoken.Trim(), StringComparison.OrdinalIgnoreCase));

    public long MinutesFor(string task) => _minutes[task];

    /// <returns>The task's new total.</returns>
    public long Add(string task, int minutes) => _minutes[task] += minutes;

    public void Clear()
    {
        foreach (var task in _tasks)
        {
            _minutes[task] = 0;
        }
    }

    /// <summary>
    /// Tasks that have any time, most time first.
    /// </summary>
    public IEnumerable<(string Task, long Minutes)> TasksWithTime() =>
        _tasks.Where(task => _minutes[task] > 0)
            .Select(task => (Task: task, Minutes: _minutes[task]))
            .OrderByDescending(entry => entry.Minutes);

    private sealed record StoredTimeCard(
        [property: JsonPropertyName("tasks")] List<string>? Tasks,
        [property: JsonPropertyName("totals")] Dictionary<string, long>? Totals);
}
