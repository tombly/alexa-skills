using AlexaSkills.Alexa;
using AlexaSkills.Skills.MajorSystem;
using AlexaSkills.Skills.TimeClock;
using AlexaSkills.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlexaSkills.Tests;

/// <summary>
/// Checks each skill against the interaction model in models/, so a renamed intent or slot fails here instead of on a device.
/// </summary>
public class InteractionModelTests
{
    // What the skills say when they couldn't read a slot.
    private static readonly string[] AsksAgain = ["sorry", "didn't hear", "please say"];

    [Theory]
    [InlineData("timeclock")]
    [InlineData("majorsystem")]
    public async Task Understands_every_intent_and_slot(string model)
    {
        Skill skill = model switch
        {
            "timeclock" => new TimeClockSkill(new InMemoryTimeCardStore(), NullLogger<TimeClockSkill>.Instance),
            "majorsystem" => new MajorSystemSkill(),
            _ => throw new ArgumentOutOfRangeException(nameof(model)),
        };
        // Each skill answers an intent it doesn't know with its help.
        var unknown = await Speech(skill, Requests.IntentRequest("NoSuchIntent"));
        var misunderstood = new List<string>();

        foreach (var request in InteractionModels.EveryIntent(InteractionModels.Load(model)))
        {
            var name = request.Request.Intent!.Name;
            var speech = await Speech(skill, request);
            if (name != "AMAZON.HelpIntent" && (speech == unknown || AsksAgain.Any(phrase => speech?.Contains(phrase, StringComparison.OrdinalIgnoreCase) == true)))
            {
                misunderstood.Add(name);
            }
        }

        Assert.Empty(misunderstood);
    }

    private static async Task<string?> Speech(Skill skill, SkillRequest request)
    {
        var speech = (await skill.HandleAsync(request, TestContext.Current.CancellationToken)).Response.OutputSpeech;
        return speech?.Text ?? speech?.Ssml;
    }
}
