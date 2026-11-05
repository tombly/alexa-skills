using System.Text.Json.Nodes;

namespace AlexaSkills.Alexa;

// https://developer.amazon.com/docs/alexa/custom-skills/request-and-response-json-reference.html#response-format

public sealed record SkillResponse(ResponseBody Response)
{
    public string Version => "1.0";
}

public sealed record ResponseBody(
    OutputSpeech? OutputSpeech = null,
    Card? Card = null,
    Reprompt? Reprompt = null,
    bool? ShouldEndSession = null,
    IReadOnlyList<Directive>? Directives = null);

public sealed record OutputSpeech(string Type, string? Text = null, string? Ssml = null)
{
    public static OutputSpeech PlainText(string text) => new("PlainText", Text: text);

    public static OutputSpeech FromSsml(string ssml) => new("SSML", Ssml: $"<speak>{ssml}</speak>");

    public static implicit operator OutputSpeech(string text) => PlainText(text);
}

public sealed record Card(string Title, string Content)
{
    public string Type => "Simple";
}

public sealed record Reprompt(OutputSpeech OutputSpeech);

/// <param name="Token">For RenderDocument: a name for the document on screen.</param>
/// <param name="Document">For RenderDocument: the APL document.</param>
/// <param name="Datasources">For RenderDocument: the data the document binds to, by name.</param>
public sealed record Directive(string Type, string? Token = null, JsonNode? Document = null, object? Datasources = null)
{
    // https://developer.amazon.com/docs/alexa/alexa-presentation-language/apl-interface.html#renderdocument-directive
    public static Directive RenderDocument(string token, JsonNode document, object datasources) =>
        new("Alexa.Presentation.APL.RenderDocument", token, document, datasources);
}

public static class Reply
{
    /// <summary>Speaks and ends the session.</summary>
    public static SkillResponse Tell(OutputSpeech speech, Card? card = null) =>
        new(new ResponseBody(speech, card, ShouldEndSession: true));

    /// <summary>Speaks and waits for an answer, repeating <paramref name="reprompt"/> if the user says nothing.</summary>
    public static SkillResponse Ask(OutputSpeech speech, OutputSpeech reprompt, Card? card = null) =>
        new(new ResponseBody(speech, card, new Reprompt(reprompt), ShouldEndSession: false));

    /// <summary>
    /// Asks the user to confirm the intent. Alexa sends it again with a ConfirmationStatus of CONFIRMED or DENIED.
    /// The intent needs a dialog model in the interaction model for this to work.
    /// </summary>
    public static SkillResponse ConfirmIntent(OutputSpeech question) =>
        new(new ResponseBody(question, Reprompt: new Reprompt(question), ShouldEndSession: false, Directives: [new Directive("Dialog.ConfirmIntent")]));

    /// <summary>
    /// Speaks and shows an APL document, only for requests where <see cref="SkillRequest.SupportsApl"/> is true.
    /// Leaving shouldEndSession unset keeps the session open without listening, so the screen stays up
    /// for about 30 seconds and the user can carry on without saying the skill's name.
    /// </summary>
    public static SkillResponse Show(OutputSpeech speech, Directive screen, Card? card = null) =>
        new(new ResponseBody(speech, card, Directives: [screen]));

    /// <summary>Ends the session without saying anything.</summary>
    public static SkillResponse End() => new(new ResponseBody(ShouldEndSession: true));

    /// <summary>For requests Alexa doesn't expect an answer to, such as SessionEndedRequest.</summary>
    public static SkillResponse Empty() => new(new ResponseBody());
}
