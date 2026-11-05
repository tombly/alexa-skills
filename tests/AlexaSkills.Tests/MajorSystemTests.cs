using AlexaSkills.Alexa;
using AlexaSkills.Skills.MajorSystem;
using AlexaSkills.Tests.TestSupport;

namespace AlexaSkills.Tests;

public class MajorSystemTests
{
    private readonly MajorSystemSkill _skill = new();

    [Theory]
    [InlineData("5", new[] { "law" })]
    [InlineData("12", new[] { "tin" })]
    [InlineData("89", new[] { "VP" })]
    [InlineData("1234", new[] { "tin", "mare" })]
    [InlineData("100", new[] { "toes", "saw" })]
    [InlineData("?", new string[0])]
    [InlineData("", new string[0])]
    public void Splits_a_number_into_words(string number, string[] expected) =>
        Assert.Equal(expected, MajorSystemWords.ForNumber(number));

    [Fact]
    public async Task Gives_a_word_for_a_number()
    {
        var response = await Send(Requests.IntentRequest("GetWordForIntent", ("mynumber", "52")));

        Assert.Contains("""<say-as interpret-as="spell-out">52</say-as>, with the word: lion.""", response.Response.OutputSpeech?.Ssml);
        Assert.Equal("You can remember 52 with the word lion", response.Response.Card?.Content);
    }

    [Fact]
    public async Task Gives_a_phrase_for_a_longer_number()
    {
        var response = await Send(Requests.IntentRequest("GetWordForIntent", ("mynumber", "1234")));

        Assert.Equal("You can remember 1234 with the phrase tin, mare", response.Response.Card?.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("?")]
    public async Task Asks_for_the_number_when_it_did_not_catch_one(string? number)
    {
        var response = await Send(Requests.IntentRequest("GetWordForIntent", ("mynumber", number)));

        Assert.False(response.Response.ShouldEndSession);
    }

    [Fact]
    public async Task Gives_the_sound_for_a_digit()
    {
        var response = await Send(Requests.IntentRequest("GetInstructionsIntent", ("mynumber", "2")));

        Assert.Contains("as in nap", response.Response.OutputSpeech?.Ssml);
        Assert.Equal("The number 2 is associated with the sound n", response.Response.Card?.Content);
    }

    [Fact]
    public async Task Asks_for_a_single_digit()
    {
        var response = await Send(Requests.IntentRequest("GetInstructionsIntent", ("mynumber", "12")));

        Assert.Equal("Please say a single digit, from zero to nine.", response.Response.OutputSpeech?.Text);
        Assert.False(response.Response.ShouldEndSession);
    }

    private Task<SkillResponse> Send(SkillRequest request) => _skill.HandleAsync(request, TestContext.Current.CancellationToken);
}
