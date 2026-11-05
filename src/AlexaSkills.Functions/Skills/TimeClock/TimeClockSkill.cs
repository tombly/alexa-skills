using System.Globalization;
using AlexaSkills.Alexa;

namespace AlexaSkills.Skills.TimeClock;

public sealed class TimeClockSkill(ITimeCardStore store) : Skill
{
    private const string CardTitle = "Time Card";
    private const string SayAgain = "please say that again";
    private const string NextHelp = "You can add time to a task, get your current times, or say help. What would you like?";
    private const string Help = "Here's some things you can say, add a ten minute break, add 2 hours and twenty minutes to meetings. tell me my hours. clear my hours. and exit.";
    private const string AfterClear = "You can add time to a task, clear all hours, or exit. What would you like?";
    private const string ClearQuestion = "Are you sure you want to clear all your hours?";

    // Keeps hours * 60 well inside an int, whatever number Alexa hears.
    private const int MaxNumber = 100_000;

    // The hours and minutes each "add" intent adds. Null means read it from the HoursNumber or MinutesNumber slot.
    private static readonly Dictionary<string, (int? Hours, int? Minutes)> AddIntents = new()
    {
        ["AddHoursIntent"] = (null, 0),
        ["AddMinutesIntent"] = (0, null),
        ["AddHoursMinutesIntent"] = (null, null),
        ["AddOneHourIntent"] = (1, 0),
        ["AddOneHourMinutesIntent"] = (1, null),
        ["AddOneHourAndAHalfIntent"] = (1, 30),
        ["AddHalfHourIntent"] = (0, 30),
    };

    public override string Name => "TimeClock";

    protected override Task<SkillResponse> OnLaunchAsync(SkillRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Reply.Ask("Time Clock, Track your time for common tasks. What can I do for you?", NextHelp));

    protected override async Task<SkillResponse> OnIntentAsync(SkillRequest request, Intent intent, CancellationToken cancellationToken)
    {
        if (intent.Name is not null && AddIntents.TryGetValue(intent.Name, out var amount))
        {
            return await AddTimeAsync(request, intent, amount, cancellationToken);
        }

        // Mid-conversation the skill suggests what to do next; one-shot requests just get the answer.
        var inConversation = !request.IsNewSession;

        return intent.Name switch
        {
            "TellTimesIntent" => TellTimes(await LoadAsync(request, cancellationToken), request.SupportsApl),
            "TellTaskTimeIntent" => TellTaskTime(await LoadAsync(request, cancellationToken), intent),
            "ClearTimeIntent" => await ClearAsync(request, intent, inConversation, cancellationToken),
            "TaskListIntent" => TaskList(),
            "AMAZON.CancelIntent" or "AMAZON.StopIntent" => inConversation
                ? Reply.Tell("Okay. Whenever you're ready, you can start adding hours for your tasks.")
                : Reply.End(),
            // Screen devices send this for "go home" or the home button while the time card is up.
            "AMAZON.NavigateHomeIntent" => Reply.End(),
            _ => Reply.Ask($"{Help} So, how can I help?", "How can I help?"),
        };
    }

    private async Task<SkillResponse> AddTimeAsync(SkillRequest request, Intent intent, (int? Hours, int? Minutes) amount, CancellationToken cancellationToken)
    {
        var hours = amount.Hours ?? ReadNumber(intent, "HoursNumber");
        if (hours is null)
        {
            return Reply.Ask($"sorry, I did not hear the hours, {SayAgain}", SayAgain);
        }

        var minutes = amount.Minutes ?? ReadNumber(intent, "MinutesNumber");
        if (minutes is null)
        {
            return Reply.Ask($"sorry, I did not hear the minutes, {SayAgain}", SayAgain);
        }

        var timeCard = await LoadAsync(request, cancellationToken);
        var task = timeCard.FindTask(intent.ResolvedSlotValue("TaskName"));
        if (task is null)
        {
            return Reply.Ask($"sorry, I did not hear the task, {SayAgain}", SayAgain);
        }

        var added = hours.Value * 60 + minutes.Value;
        var total = timeCard.Add(task, added);
        await store.SaveAsync(UserId(request), timeCard, cancellationToken);
        return Reply.Tell($"added {TimePhrases.ForMinutes(added)} for {task}. {task} has a total of {TimePhrases.ForMinutes(total)}");
    }

    private async Task<SkillResponse> ClearAsync(SkillRequest request, Intent intent, bool inConversation, CancellationToken cancellationToken)
    {
        // A misheard "clear" would wipe every task, so only clear once the user has said yes.
        // The interaction model has Alexa ask first; this asks if a request arrives without an answer.
        switch (intent.ConfirmationStatus)
        {
            case "DENIED":
                return inConversation
                    ? Reply.Ask($"okay, I didn't clear anything. {NextHelp}", NextHelp)
                    : Reply.Tell("okay, I didn't clear anything");
            case not "CONFIRMED":
                return Reply.ConfirmIntent(ClearQuestion);
        }

        var timeCard = await LoadAsync(request, cancellationToken);
        timeCard.Clear();
        await store.SaveAsync(UserId(request), timeCard, cancellationToken);
        return inConversation
            ? Reply.Ask($"cleared all your hours. {AfterClear}", AfterClear)
            : Reply.Tell("cleared all your hours");
    }

    private static SkillResponse TellTimes(TimeCard timeCard, bool onScreen)
    {
        var totals = timeCard.TasksWithTime()
            .Select(entry => (entry.Task, Time: TimePhrases.ForMinutes(entry.Minutes)))
            .ToList();

        var (speech, card) = totals.Count == 0
            ? ("No tasks have any hours yet", new Card(CardTitle, "All tasks have zero hours"))
            : (string.Join(", ", totals.Select(entry => $"{entry.Task}: {entry.Time}")),
                new Card(CardTitle, string.Join('\n', totals.Select(entry => $"{entry.Task} : {entry.Time}"))));

        return onScreen ? Reply.Show(speech, TimeCardScreen.For(timeCard), card) : Reply.Tell(speech, card);
    }

    private static SkillResponse TellTaskTime(TimeCard timeCard, Intent intent)
    {
        var task = timeCard.FindTask(intent.ResolvedSlotValue("TaskName"));
        if (task is null)
        {
            return Reply.Ask($"sorry, I did not hear the task, {SayAgain}", SayAgain);
        }

        var minutes = timeCard.MinutesFor(task);
        if (minutes == 0)
        {
            return Reply.Tell($"{task} has no time", new Card(CardTitle, $"{task} : no time"));
        }

        var total = $"{task} has a total of {TimePhrases.ForMinutes(minutes)}";
        return Reply.Tell(total, new Card(CardTitle, total));
    }

    private static SkillResponse TaskList()
    {
        var tasks = TimeCard.TaskNames;
        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        return Reply.Tell(
            $"you can track time for: {string.Join(", ", tasks.Take(tasks.Count - 1))}, and {tasks[^1]}",
            new Card("Available Tasks", string.Join(", ", tasks.Select(textInfo.ToTitleCase))));
    }

    private Task<TimeCard> LoadAsync(SkillRequest request, CancellationToken cancellationToken) =>
        store.LoadAsync(UserId(request), cancellationToken);

    private static string UserId(SkillRequest request) =>
        request.UserId ?? throw new InvalidOperationException("The Alexa request has no user ID.");

    // AMAZON.NUMBER slots hold plain digits, or "?" when Alexa didn't catch the number.
    private static int? ReadNumber(Intent intent, string slot) =>
        int.TryParse(intent.SlotValue(slot), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number <= MaxNumber
            ? number
            : null;
}
