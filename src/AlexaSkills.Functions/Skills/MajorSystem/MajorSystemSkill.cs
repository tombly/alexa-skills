using AlexaSkills.Alexa;

namespace AlexaSkills.Skills.MajorSystem;

public sealed class MajorSystemSkill : Skill
{
    private const string SkillName = "Major System";
    private const string Help = "You can say How does this work? or How can I remember fifty two, or, What sound is two associated with six. What can I help you with?";
    private const string HelpReprompt = "What can I help you with?";
    private const string NumberReprompt = "Which number do you want to remember?";
    private const string HowItWorks = "The Major System is a technique for easily remembering numbers. The way it works is that each digit from 0 to 9 is assigned a consonant sound and then you add vowel sounds to form words, so remembering the number is as easy as remembering the words. For example, to remember the number twelve, since one is associated with the sound tee, and two is associated with the sound en, you can add the vowel I, and remember the number twelve with the word TIN. If you are getting started with the Major System, you can ask what the sounds are for different digits, for example: Say, what is the sound for two?";

    // What Alexa says (SSML) and what the card shows for each digit's sounds.
    private static readonly (string Speech, string Card)[] DigitSounds =
    [
        ("""sounds, <phoneme alphabet="ipa" ph="sss">s</phoneme>, and, <phoneme alphabet="ipa" ph="zzz">z</phoneme>, as in see, and zero""", "sounds s and z"),
        ("""sounds, <phoneme alphabet="ipa" ph="tah">t</phoneme>, and, <phoneme alphabet="ipa" ph="dah">d</phoneme>, as in trap, and dig""", "sounds t and d"),
        ("""sound, <phoneme alphabet="ipa" ph="nah">n</phoneme>, as in nap""", "sound n"),
        ("""sound, <phoneme alphabet="ipa" ph="mmah">m</phoneme>, as in mouse""", "sound m"),
        ("""sound, <phoneme alphabet="ipa" ph="rrah">r</phoneme>, as in red""", "sound r"),
        ("""sound, <phoneme alphabet="ipa" ph="llah">l</phoneme>, as in lay""", "sound l"),
        ("""sounds, <phoneme alphabet="ipa" ph="t͡ʃ">ch</phoneme>. <phoneme alphabet="ipa" ph="d͡ʒ">soft g</phoneme>. <phoneme alphabet="ipa" ph="ʃ">sh</phoneme>. and, <phoneme alphabet="ipa" ph="ʒ">j</phoneme>. as in ship, or chart""", "sounds ch, soft g, sh, and j"),
        ("""sounds, <phoneme alphabet="ipa" ph="kah">k</phoneme>, and, <phoneme alphabet="ipa" ph="gah">g</phoneme>, as in cat, or game""", "sounds k, hard c, hard g, q, and qu"),
        ("""sounds, <phoneme alphabet="ipa" ph="fah">f</phoneme>, and <phoneme alphabet="ipa" ph="vah">v</phoneme>, as in five, or vest""", "sound f and v"),
        ("""sounds, <phoneme alphabet="ipa" ph="pah">p</phoneme>, and <phoneme alphabet="ipa" ph="bah">b</phoneme>, as in peak, or bed""", "sounds p and b"),
    ];

    public override string Name => "MajorSystem";

    protected override Task<SkillResponse> OnLaunchAsync(SkillRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Reply.Ask("Major System, What can I help you with?", Help));

    protected override Task<SkillResponse> OnIntentAsync(SkillRequest request, Intent intent, CancellationToken cancellationToken) =>
        Task.FromResult(intent.Name switch
        {
            "GetWordForIntent" => WordsForNumber(intent.SlotValue("mynumber")),
            "GetInstructionsIntent" => SoundsForDigit(intent.SlotValue("mynumber")),
            "HowItWorksIntent" => Reply.Ask(HowItWorks, HelpReprompt),
            "AMAZON.CancelIntent" or "AMAZON.StopIntent" => Reply.Tell("Goodbye!"),
            _ => Reply.Ask(Help, HelpReprompt),
        });

    private static SkillResponse WordsForNumber(string? number)
    {
        if (number is null)
        {
            return Reply.Ask("Please say the number that you want to remember, for example: two five three", NumberReprompt);
        }

        // AMAZON.NUMBER slots hold "?" when Alexa didn't catch the number.
        var words = MajorSystemWords.ForNumber(number);
        if (words.Count == 0)
        {
            return Reply.Ask("Sorry, I didn't catch the number. Please say it again, for example: two five three", NumberReprompt);
        }

        var kind = words.Count == 1 ? "word" : "phrase";
        var list = string.Join(", ", words);
        var spelledOut = string.Concat(words.Select(word => $"""<say-as interpret-as="spell-out">{word}</say-as>, """));
        var speech = $"""You can remember the number, <say-as interpret-as="spell-out">{number}</say-as>, with the {kind}: {list}. {spelledOut}""";
        return Reply.Tell(OutputSpeech.FromSsml(speech), new Card(SkillName, $"You can remember {number} with the {kind} {list}"));
    }

    private static SkillResponse SoundsForDigit(string? value)
    {
        if (!int.TryParse(value, out var digit))
        {
            return Reply.Ask("I didn't hear the number, please say that again", "Say that again");
        }

        if (digit is < 0 or > 9)
        {
            return Reply.Ask("Please say a single digit, from zero to nine.", "Which digit?");
        }

        var (speech, card) = DigitSounds[digit];
        return Reply.Tell(
            OutputSpeech.FromSsml($"The number {digit} is associated with the {speech}"),
            new Card(SkillName, $"The number {digit} is associated with the {card}"));
    }
}
