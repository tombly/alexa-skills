using System.Text.Json;
using System.Text.Json.Nodes;
using AlexaSkills.Alexa;

namespace AlexaSkills.Tests;

public class SkillJsonTests
{
    [Fact]
    public void Responses_use_the_Alexa_response_format()
    {
        var json = JsonSerializer.Serialize(Reply.Ask("Hello", "Still there?", new Card("Title", "Content")), AlexaJson.Options);

        AssertJsonEqual("""
            {
              "version": "1.0",
              "response": {
                "outputSpeech": { "type": "PlainText", "text": "Hello" },
                "card": { "type": "Simple", "title": "Title", "content": "Content" },
                "reprompt": { "outputSpeech": { "type": "PlainText", "text": "Still there?" } },
                "shouldEndSession": false
              }
            }
            """, json);
    }

    [Fact]
    public void Confirmation_questions_carry_the_dialog_directive()
    {
        var json = JsonSerializer.Serialize(Reply.ConfirmIntent("Are you sure?"), AlexaJson.Options);

        AssertJsonEqual("""
            {
              "version": "1.0",
              "response": {
                "outputSpeech": { "type": "PlainText", "text": "Are you sure?" },
                "reprompt": { "outputSpeech": { "type": "PlainText", "text": "Are you sure?" } },
                "shouldEndSession": false,
                "directives": [ { "type": "Dialog.ConfirmIntent" } ]
              }
            }
            """, json);
    }

    [Fact]
    public void Ssml_is_wrapped_in_speak_tags()
    {
        var json = JsonSerializer.Serialize(Reply.Tell(OutputSpeech.FromSsml("""<say-as interpret-as="spell-out">12</say-as>""")), AlexaJson.Options);

        AssertJsonEqual("""
            {
              "version": "1.0",
              "response": {
                "outputSpeech": { "type": "SSML", "ssml": "<speak><say-as interpret-as=\"spell-out\">12</say-as></speak>" },
                "shouldEndSession": true
              }
            }
            """, json);
    }

    [Fact]
    public void Reads_an_intent_request_with_slots_and_entity_resolution()
    {
        const string json = """
            {
              "version": "1.0",
              "session": {
                "new": false,
                "sessionId": "amzn1.echo-api.session.test",
                "application": { "applicationId": "amzn1.ask.skill.test" },
                "attributes": {},
                "user": { "userId": "amzn1.ask.account.TEST" }
              },
              "context": {
                "System": {
                  "application": { "applicationId": "amzn1.ask.skill.test" },
                  "user": { "userId": "amzn1.ask.account.TEST" },
                  "device": { "deviceId": "amzn1.ask.device.test", "supportedInterfaces": {} }
                }
              },
              "request": {
                "type": "IntentRequest",
                "requestId": "amzn1.echo-api.request.test",
                "timestamp": "2026-11-01T17:04:05Z",
                "locale": "en-US",
                "intent": {
                  "name": "AddMinutesIntent",
                  "confirmationStatus": "NONE",
                  "slots": {
                    "MinutesNumber": { "name": "MinutesNumber", "value": "15", "confirmationStatus": "NONE" },
                    "TaskName": {
                      "name": "TaskName",
                      "value": "lunch break",
                      "resolutions": {
                        "resolutionsPerAuthority": [
                          {
                            "authority": "amzn1.er-authority.echo-sdk.amzn1.ask.skill.test.LIST_OF_TASK_NAMES",
                            "status": { "code": "ER_SUCCESS_MATCH" },
                            "values": [ { "value": { "name": "lunch", "id": "LUNCH" } } ]
                          }
                        ]
                      }
                    }
                  }
                }
              }
            }
            """;

        var request = JsonSerializer.Deserialize<SkillRequest>(json, AlexaJson.Options)!;

        Assert.Equal("amzn1.ask.skill.test", request.ApplicationId);
        Assert.Equal("amzn1.ask.account.TEST", request.UserId);
        Assert.False(request.IsNewSession);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 17, 4, 5, TimeSpan.Zero), request.Request.Timestamp);
        Assert.Equal("AddMinutesIntent", request.Request.Intent?.Name);
        Assert.Equal("NONE", request.Request.Intent?.ConfirmationStatus);
        Assert.Equal("15", request.Request.Intent?.SlotValue("MinutesNumber"));
        Assert.Equal("lunch", request.Request.Intent?.ResolvedSlotValue("TaskName"));
        Assert.Equal("15", request.Request.Intent?.ResolvedSlotValue("MinutesNumber"));
        Assert.False(request.SupportsApl);
    }

    [Fact]
    public void Reads_whether_the_device_can_show_apl()
    {
        const string json = """
            {
              "version": "1.0",
              "context": {
                "System": {
                  "device": {
                    "deviceId": "amzn1.ask.device.test",
                    "supportedInterfaces": { "Alexa.Presentation.APL": { "runtime": { "maxVersion": "2024.3" } } }
                  }
                }
              },
              "request": { "type": "LaunchRequest", "requestId": "amzn1.echo-api.request.test", "timestamp": "2026-11-01T17:04:05Z", "locale": "en-US" }
            }
            """;

        Assert.True(JsonSerializer.Deserialize<SkillRequest>(json, AlexaJson.Options)!.SupportsApl);
    }

    [Fact]
    public void Screens_carry_the_apl_document_and_leave_the_session_open()
    {
        var screen = Directive.RenderDocument("token", JsonNode.Parse("""{ "type": "APL" }""")!, new { list = new { title = "Title" } });

        var json = JsonSerializer.Serialize(Reply.Show("Hello", screen), AlexaJson.Options);

        AssertJsonEqual("""
            {
              "version": "1.0",
              "response": {
                "outputSpeech": { "type": "PlainText", "text": "Hello" },
                "directives": [
                  {
                    "type": "Alexa.Presentation.APL.RenderDocument",
                    "token": "token",
                    "document": { "type": "APL" },
                    "datasources": { "list": { "title": "Title" } }
                  }
                ]
              }
            }
            """, json);
    }

    [Fact]
    public void Reads_why_a_session_ended()
    {
        const string json = """
            {
              "version": "1.0",
              "request": {
                "type": "SessionEndedRequest",
                "requestId": "amzn1.echo-api.request.test",
                "timestamp": "2026-11-01T17:04:05Z",
                "locale": "en-US",
                "reason": "ERROR",
                "error": { "type": "INVALID_RESPONSE", "message": "An exception occurred while dispatching the request to the skill." }
              }
            }
            """;

        var request = JsonSerializer.Deserialize<SkillRequest>(json, AlexaJson.Options)!;

        Assert.Equal("ERROR", request.Request.Reason);
        Assert.Equal(new SkillError("INVALID_RESPONSE", "An exception occurred while dispatching the request to the skill."), request.Request.Error);
    }

    private static void AssertJsonEqual(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), actual);
}
