using System.Text.Json;
using AlexaSkills.Skills.TimeClock;
using AlexaSkills.Tests.TestSupport;

namespace AlexaSkills.Tests;

/// <summary>
/// Checks Time Clock's task list and dialog against models/timeclock.json. InteractionModelTests checks its intents.
/// </summary>
public class TimeClockModelTests
{
    private static readonly JsonElement InteractionModel = InteractionModels.Load("timeclock");

    private static readonly (string Value, string[] Synonyms)[] TaskValues = InteractionModel.GetProperty("languageModel").GetProperty("types").EnumerateArray()
        .Single(type => type.GetProperty("name").GetString() == "LIST_OF_TASK_NAMES")
        .GetProperty("values").EnumerateArray()
        .Select(value => value.GetProperty("name"))
        .Select(name => (
            name.GetProperty("value").GetString()!.Trim(),
            name.TryGetProperty("synonyms", out var synonyms) ? synonyms.EnumerateArray().Select(synonym => synonym.GetString()!.Trim()).ToArray() : []))
        .ToArray();

    [Fact]
    public void Task_names_match_the_slot_values()
    {
        Assert.Equal(TimeCard.TaskNames.Order(), TaskValues.Select(value => value.Value).Order());
    }

    [Fact]
    public void Each_word_means_one_task()
    {
        // Alexa resolves a synonym to the value it's listed under, so a word under two values would be a coin toss.
        var repeated = TaskValues.SelectMany(value => value.Synonyms.Prepend(value.Value))
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(repeated);
    }

    [Fact]
    public void Alexa_confirms_before_clearing()
    {
        var clear = InteractionModel.GetProperty("dialog").GetProperty("intents").EnumerateArray()
            .Single(intent => intent.GetProperty("name").GetString() == "ClearTimeIntent");
        var promptId = clear.GetProperty("prompts").GetProperty("confirmation").GetString();

        Assert.True(clear.GetProperty("confirmationRequired").GetBoolean());
        Assert.Contains(InteractionModel.GetProperty("prompts").EnumerateArray(), prompt => prompt.GetProperty("id").GetString() == promptId);
    }
}
