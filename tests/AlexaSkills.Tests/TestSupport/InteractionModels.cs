using System.Text.Json;
using AlexaSkills.Alexa;

namespace AlexaSkills.Tests.TestSupport;

/// <summary>
/// The interaction models in models/, which get pasted into the Alexa developer console's JSON Editor.
/// </summary>
static class InteractionModels
{
    public static JsonElement Load(string name) => JsonDocument
        .Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "models", $"{name}.json")))
        .RootElement.GetProperty("interactionModel");

    /// <summary>
    /// A request for each intent in the model, with every slot filled: 1 for a number, otherwise the slot type's first value.
    /// </summary>
    public static IEnumerable<SkillRequest> EveryIntent(JsonElement model)
    {
        var languageModel = model.GetProperty("languageModel");
        var firstValues = languageModel.GetProperty("types").EnumerateArray().ToDictionary(
            type => type.GetProperty("name").GetString()!,
            type => type.GetProperty("values")[0].GetProperty("name").GetProperty("value").GetString()!.Trim());

        foreach (var intent in languageModel.GetProperty("intents").EnumerateArray())
        {
            var slots = intent.TryGetProperty("slots", out var slotList)
                ? slotList.EnumerateArray()
                    .Select(slot => (slot.GetProperty("name").GetString()!, slot.GetProperty("type").GetString()))
                    .Select(slot => (slot.Item1, (string?)(slot.Item2 == "AMAZON.NUMBER" ? "1" : firstValues[slot.Item2!])))
                    .ToArray()
                : [];
            yield return Requests.IntentRequest(intent.GetProperty("name").GetString()!, slots);
        }
    }
}
