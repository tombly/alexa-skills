namespace AlexaSkills.Skills.TimeClock;

public static class TimePhrases
{
    /// <summary>
    /// How Alexa says an amount of time, e.g. 140 minutes is "2 hours and 20 minutes".
    /// </summary>
    public static string ForMinutes(long totalMinutes)
    {
        var (hours, minutes) = Math.DivRem(totalMinutes, 60);
        if (hours == 0 && minutes == 0)
        {
            return "no time";
        }

        var parts = new List<string>(2);
        if (hours > 0)
        {
            parts.Add(hours == 1 ? "an hour" : $"{hours} hours");
        }
        if (minutes > 0)
        {
            parts.Add(minutes == 1 ? "one minute" : $"{minutes} minutes");
        }
        return string.Join(" and ", parts);
    }
}
