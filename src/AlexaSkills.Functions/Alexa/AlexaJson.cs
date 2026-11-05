using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlexaSkills.Alexa;

public static class AlexaJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
