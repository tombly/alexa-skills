using AlexaSkills.Skills.TimeClock;

namespace AlexaSkills.Tests;

public class TimePhrasesTests
{
    [Theory]
    [InlineData(0, "no time")]
    [InlineData(1, "one minute")]
    [InlineData(5, "5 minutes")]
    [InlineData(60, "an hour")]
    [InlineData(61, "an hour and one minute")]
    [InlineData(90, "an hour and 30 minutes")]
    [InlineData(120, "2 hours")]
    [InlineData(140, "2 hours and 20 minutes")]
    public void Says_minutes_the_way_the_original_skill_did(int minutes, string expected) =>
        Assert.Equal(expected, TimePhrases.ForMinutes(minutes));
}
