namespace AlexaSkills.Skills.MajorSystem;

/// <summary>
/// Turns a number into Major System words, e.g. 1234 becomes "tin, mare".
/// </summary>
public static class MajorSystemWords
{
    private static readonly Dictionary<string, string> WordsByNumber = new()
    {
        ["0"] = "saw", ["1"] = "tie", ["2"] = "knee", ["3"] = "mow", ["4"] = "raw",
        ["5"] = "law", ["6"] = "joy", ["7"] = "key", ["8"] = "fee", ["9"] = "pie",
        ["10"] = "toes", ["11"] = "toad", ["12"] = "tin", ["13"] = "dam", ["14"] = "tire",
        ["15"] = "tile", ["16"] = "ditch", ["17"] = "tack", ["18"] = "dove", ["19"] = "tub",
        ["20"] = "nose", ["21"] = "nut", ["22"] = "nun", ["23"] = "name", ["24"] = "nero",
        ["25"] = "nail", ["26"] = "hinge", ["27"] = "neck", ["28"] = "knife", ["29"] = "knob",
        ["30"] = "moose", ["31"] = "moat", ["32"] = "man", ["33"] = "mime", ["34"] = "mare",
        ["35"] = "mail", ["36"] = "match", ["37"] = "mug", ["38"] = "movie", ["39"] = "mop",
        ["40"] = "rice", ["41"] = "rat", ["42"] = "rhino", ["43"] = "ram", ["44"] = "rare",
        ["45"] = "rail", ["46"] = "roach", ["47"] = "rake", ["48"] = "roof", ["49"] = "rope",
        ["50"] = "lasso", ["51"] = "lid", ["52"] = "lion", ["53"] = "lamb", ["54"] = "lure",
        ["55"] = "lily", ["56"] = "leech", ["57"] = "lock", ["58"] = "leaf", ["59"] = "leap",
        ["60"] = "cheese", ["61"] = "cheetah", ["62"] = "chain", ["63"] = "jam", ["64"] = "jar",
        ["65"] = "jewel", ["66"] = "judge", ["67"] = "jack", ["68"] = "chief", ["69"] = "jeep",
        ["70"] = "case", ["71"] = "cat", ["72"] = "cone", ["73"] = "comb", ["74"] = "cry",
        ["75"] = "coal", ["76"] = "couch", ["77"] = "cake", ["78"] = "coffee", ["79"] = "cap",
        ["80"] = "face", ["81"] = "fat", ["82"] = "fun", ["83"] = "foam", ["84"] = "fur",
        ["85"] = "file", ["86"] = "fish", ["87"] = "fig", ["88"] = "fife", ["89"] = "VP",
        ["90"] = "bus", ["91"] = "bat", ["92"] = "bone", ["93"] = "bum", ["94"] = "bear",
        ["95"] = "bell", ["96"] = "beach", ["97"] = "book", ["98"] = "puff", ["99"] = "pipe",
    };

    private static readonly int LongestNumber = WordsByNumber.Keys.Max(number => number.Length);

    /// <summary>
    /// Matches the longest known number from the left, then the next, and so on.
    /// Returns no words if <paramref name="number"/> isn't all digits.
    /// </summary>
    public static IReadOnlyList<string> ForNumber(string number)
    {
        if (number.Length == 0 || !number.All(char.IsAsciiDigit))
        {
            return [];
        }

        var words = new List<string>();
        var start = 0;
        while (start < number.Length)
        {
            // Every single digit has a word, so this always finds one by length 1.
            var length = Math.Min(LongestNumber, number.Length - start);
            string? word;
            while (!WordsByNumber.TryGetValue(number.Substring(start, length), out word))
            {
                length--;
            }

            words.Add(word);
            start += length;
        }
        return words;
    }
}
