using System.Text.Json;
using System.Text.Json.Nodes;
using AlexaSkills.Alexa;
using AlexaSkills.Skills.TimeClock;
using AlexaSkills.Tests.TestSupport;

namespace AlexaSkills.Tests;

public class TimeClockTests
{
    private readonly InMemoryTimeCardStore _store = new();
    private readonly TimeClockSkill _skill;

    public TimeClockTests() => _skill = new TimeClockSkill(_store);

    [Fact]
    public async Task Adds_hours_and_minutes_to_a_task()
    {
        var response = await Send(Requests.IntentRequest("AddHoursMinutesIntent", ("TaskName", "meetings"), ("HoursNumber", "2"), ("MinutesNumber", "20")));

        Assert.Equal("added 2 hours and 20 minutes for meetings. meetings has a total of 2 hours and 20 minutes", response.Response.OutputSpeech?.Text);
        Assert.True(response.Response.ShouldEndSession);
        Assert.Equal(140, _store.Load(Requests.UserId).MinutesFor("meetings"));
    }

    [Theory]
    [InlineData("AddOneHourIntent", 60)]
    [InlineData("AddHalfHourIntent", 30)]
    [InlineData("AddOneHourAndAHalfIntent", 90)]
    public async Task Fixed_amount_intents_add_their_amount(string intent, int minutes)
    {
        await Send(Requests.IntentRequest(intent, ("TaskName", "email")));

        Assert.Equal(minutes, _store.Load(Requests.UserId).MinutesFor("email"));
    }

    [Fact]
    public async Task Adds_an_hour_and_some_minutes()
    {
        await Send(Requests.IntentRequest("AddOneHourMinutesIntent", ("TaskName", "travel"), ("MinutesNumber", "15")));

        Assert.Equal(75, _store.Load(Requests.UserId).MinutesFor("travel"));
    }

    [Fact]
    public async Task Totals_add_up_across_requests()
    {
        await Send(Requests.IntentRequest("AddMinutesIntent", ("TaskName", "email"), ("MinutesNumber", "10")));
        var response = await Send(Requests.IntentRequest("AddMinutesIntent", ("TaskName", "email"), ("MinutesNumber", "10")));

        Assert.Equal("added 10 minutes for email. email has a total of 20 minutes", response.Response.OutputSpeech?.Text);
    }

    [Theory]
    [InlineData("?")]
    [InlineData("-5")]
    [InlineData(null)]
    public async Task Asks_again_when_it_did_not_catch_the_number(string? hours)
    {
        var response = await Send(Requests.IntentRequest("AddHoursIntent", ("TaskName", "email"), ("HoursNumber", hours)));

        Assert.Equal("sorry, I did not hear the hours, please say that again", response.Response.OutputSpeech?.Text);
        Assert.False(response.Response.ShouldEndSession);
        Assert.Empty(_store.Rows);
    }

    [Fact]
    public async Task Asks_again_for_a_task_it_does_not_know()
    {
        var response = await Send(Requests.IntentRequest("AddOneHourIntent", ("TaskName", "gardening")));

        Assert.Equal("sorry, I did not hear the task, please say that again", response.Response.OutputSpeech?.Text);
        Assert.Empty(_store.Rows);
    }

    [Fact]
    public async Task Matches_tasks_regardless_of_case_and_spacing()
    {
        await Send(Requests.IntentRequest("AddHalfHourIntent", ("TaskName", " Phone Calls ")));

        Assert.Equal(30, _store.Load(Requests.UserId).MinutesFor("phone calls"));
    }

    [Fact]
    public async Task Reads_back_times_with_the_most_first()
    {
        await Send(Requests.IntentRequest("AddMinutesIntent", ("TaskName", "email"), ("MinutesNumber", "10")));
        await Send(Requests.IntentRequest("AddHoursMinutesIntent", ("TaskName", "meetings"), ("HoursNumber", "2"), ("MinutesNumber", "20")));

        var response = await Send(Requests.IntentRequest("TellTimesIntent"));

        Assert.Equal("meetings: 2 hours and 20 minutes, email: 10 minutes", response.Response.OutputSpeech?.Text);
        Assert.Equal(new Card("Time Card", "meetings : 2 hours and 20 minutes\nemail : 10 minutes"), response.Response.Card);
        Assert.Null(response.Response.Directives);
        Assert.True(response.Response.ShouldEndSession);
    }

    [Fact]
    public async Task Shows_the_time_card_on_a_screen()
    {
        await Send(Requests.IntentRequest("AddMinutesIntent", ("TaskName", "email"), ("MinutesNumber", "10")));
        await Send(Requests.IntentRequest("AddHoursMinutesIntent", ("TaskName", "data entry"), ("HoursNumber", "2"), ("MinutesNumber", "20")));

        var response = await Send(Requests.IntentRequest("TellTimesIntent").OnScreen());

        var screen = Assert.Single(response.Response.Directives!);
        Assert.Equal("Alexa.Presentation.APL.RenderDocument", screen.Type);
        AssertJsonEqual("""
            {
              "timeCard": {
                "title": "Time Card",
                "subtitle": "Total: 2 hr 30 min",
                "items": [
                  { "primaryText": "Data Entry", "secondaryText": "2 hr 20 min" },
                  { "primaryText": "Email", "secondaryText": "10 min" }
                ]
              }
            }
            """, JsonSerializer.Serialize(screen.Datasources, AlexaJson.Options));
        // Alexa still reads the hours out, and leaving the session open keeps the list on screen.
        Assert.Equal("data entry: 2 hours and 20 minutes, email: 10 minutes", response.Response.OutputSpeech?.Text);
        Assert.Null(response.Response.ShouldEndSession);
    }

    [Fact]
    public async Task Shows_an_empty_time_card_on_a_screen()
    {
        var response = await Send(Requests.IntentRequest("TellTimesIntent").OnScreen());

        var timeCard = JsonSerializer.SerializeToNode(Assert.Single(response.Response.Directives!).Datasources, AlexaJson.Options)!["timeCard"]!;
        Assert.Equal("No tasks have any hours yet", (string?)timeCard["subtitle"]);
        Assert.Empty(timeCard["items"]!.AsArray());
    }

    [Fact]
    public void The_screen_document_takes_the_time_card_data_source()
    {
        var screen = TimeCardScreen.For(TimeCard.FromJson(null));

        var parameters = screen.Document!["mainTemplate"]!["parameters"]!.AsArray().Select(parameter => (string?)parameter);
        var dataSources = JsonSerializer.SerializeToNode(screen.Datasources, AlexaJson.Options)!.AsObject().Select(property => property.Key);
        Assert.Equal("APL", (string?)screen.Document["type"]);
        Assert.Equal(parameters, dataSources);
    }

    [Fact]
    public async Task Says_when_no_task_has_time()
    {
        var response = await Send(Requests.IntentRequest("TellTimesIntent"));

        Assert.Equal("No tasks have any hours yet", response.Response.OutputSpeech?.Text);
    }

    [Fact]
    public async Task Tells_the_time_for_one_task()
    {
        await Send(Requests.IntentRequest("AddOneHourIntent", ("TaskName", "lunch")));

        var withTime = await Send(Requests.IntentRequest("TellTaskTimeIntent", ("TaskName", "lunch")));
        var withoutTime = await Send(Requests.IntentRequest("TellTaskTimeIntent", ("TaskName", "mail")));

        Assert.Equal("lunch has a total of an hour", withTime.Response.OutputSpeech?.Text);
        Assert.Equal("mail has no time", withoutTime.Response.OutputSpeech?.Text);
    }

    [Fact]
    public async Task Asks_before_clearing()
    {
        await Send(Requests.IntentRequest("AddOneHourIntent", ("TaskName", "work")));

        var response = await Send(Requests.IntentRequest("ClearTimeIntent"));

        Assert.Equal("Are you sure you want to clear all your hours?", response.Response.OutputSpeech?.Text);
        Assert.Equal("Dialog.ConfirmIntent", Assert.Single(response.Response.Directives!).Type);
        Assert.False(response.Response.ShouldEndSession);
        Assert.Equal(60, _store.Load(Requests.UserId).MinutesFor("work"));
    }

    [Fact]
    public async Task Clearing_as_a_one_shot_just_confirms()
    {
        await Send(Requests.IntentRequest("AddOneHourIntent", ("TaskName", "work")));

        var response = await Send(Requests.IntentRequest("ClearTimeIntent").Answered("CONFIRMED"));

        Assert.Equal("cleared all your hours", response.Response.OutputSpeech?.Text);
        Assert.True(response.Response.ShouldEndSession);
        Assert.Equal(0, _store.Load(Requests.UserId).MinutesFor("work"));
    }

    [Fact]
    public async Task Clearing_mid_conversation_offers_what_to_do_next()
    {
        var response = await Send(Requests.IntentRequest("ClearTimeIntent").Answered("CONFIRMED").InConversation());

        Assert.False(response.Response.ShouldEndSession);
    }

    [Fact]
    public async Task Keeps_the_hours_when_the_user_says_no()
    {
        await Send(Requests.IntentRequest("AddOneHourIntent", ("TaskName", "work")));

        var oneShot = await Send(Requests.IntentRequest("ClearTimeIntent").Answered("DENIED"));
        var inConversation = await Send(Requests.IntentRequest("ClearTimeIntent").Answered("DENIED").InConversation());

        Assert.Equal("okay, I didn't clear anything", oneShot.Response.OutputSpeech?.Text);
        Assert.True(oneShot.Response.ShouldEndSession);
        Assert.False(inConversation.Response.ShouldEndSession);
        Assert.Equal(60, _store.Load(Requests.UserId).MinutesFor("work"));
    }

    [Fact]
    public async Task Stopping_a_one_shot_ends_without_speaking()
    {
        var response = await Send(Requests.IntentRequest("AMAZON.StopIntent"));

        Assert.Null(response.Response.OutputSpeech);
        Assert.True(response.Response.ShouldEndSession);
    }

    [Fact]
    public async Task Lists_the_tasks()
    {
        var response = await Send(Requests.IntentRequest("TaskListIntent"));

        Assert.StartsWith("you can track time for: accounting, administrative, break,", response.Response.OutputSpeech?.Text);
        Assert.EndsWith("writing, and work", response.Response.OutputSpeech?.Text);
        Assert.Contains("Customer Service", response.Response.Card?.Content);
    }

    [Fact]
    public async Task Reads_rows_saved_before_a_task_was_added()
    {
        // A row saved before "work" was on the task list.
        _store.Rows[Requests.UserId] = """{"tasks":["administrative","break","meetings"],"totals":{"administrative":0,"break":15,"meetings":90}}""";

        var times = await Send(Requests.IntentRequest("TellTimesIntent"));
        var added = await Send(Requests.IntentRequest("AddOneHourIntent", ("TaskName", "work")));

        Assert.Equal("meetings: an hour and 30 minutes, break: 15 minutes", times.Response.OutputSpeech?.Text);
        Assert.Equal("added an hour for work. work has a total of an hour", added.Response.OutputSpeech?.Text);
        Assert.Equal(90, _store.Load(Requests.UserId).MinutesFor("meetings"));
    }

    [Fact]
    public async Task Reads_totals_too_big_for_an_int()
    {
        // A couple of saved rows hold nonsense totals this big.
        _store.Rows[Requests.UserId] = """{"tasks":["work"],"totals":{"work":60000000000000}}""";

        var time = await Send(Requests.IntentRequest("TellTaskTimeIntent", ("TaskName", "work")));

        Assert.Equal("work has a total of 1000000000000 hours", time.Response.OutputSpeech?.Text);
    }

    private Task<SkillResponse> Send(SkillRequest request) => _skill.HandleAsync(request, TestContext.Current.CancellationToken);

    private static void AssertJsonEqual(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), actual);
}
