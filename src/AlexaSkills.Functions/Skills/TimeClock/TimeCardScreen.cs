using System.Globalization;
using System.Text.Json.Nodes;
using AlexaSkills.Alexa;

namespace AlexaSkills.Skills.TimeClock;

/// <summary>
/// The time card on a device with a screen: the total, then each task with time, most first.
/// The layout is TimeCardScreen.json, an APL document built on Amazon's AlexaTextList.
/// </summary>
public static class TimeCardScreen
{
    public const string Token = "timecard";

    private static readonly JsonNode Document = JsonNode.Parse(
        typeof(TimeCardScreen).Assembly.GetManifestResourceStream("TimeCardScreen.json")
        ?? throw new InvalidOperationException("TimeCardScreen.json isn't embedded in the app."))!;

    public static Directive For(TimeCard timeCard)
    {
        var tasks = timeCard.TasksWithTime().ToList();
        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        var subtitle = tasks.Count == 0
            ? "No tasks have any hours yet"
            : $"Total: {TimePhrases.ForScreen(tasks.Sum(task => task.Minutes))}";
        var items = tasks.Select(task => new ListItem(textInfo.ToTitleCase(task.Task), TimePhrases.ForScreen(task.Minutes))).ToList();

        // The document's mainTemplate takes one parameter, timeCard, which Alexa fills from the data source of that name.
        return Directive.RenderDocument(Token, Document, new { timeCard = new TimeCardData("Time Card", subtitle, items) });
    }

    private sealed record TimeCardData(string Title, string Subtitle, IReadOnlyList<ListItem> Items);

    private sealed record ListItem(string PrimaryText, string SecondaryText);
}
