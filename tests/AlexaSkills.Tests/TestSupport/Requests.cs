using System.Text;
using AlexaSkills.Alexa;

namespace AlexaSkills.Tests.TestSupport;

static class Requests
{
    public const string SkillId = "amzn1.ask.skill.00000000-0000-0000-0000-000000000000";
    public const string UserId = "amzn1.ask.account.TESTUSER";

    public static SkillRequest LaunchRequest() =>
        Create(new Request("LaunchRequest", DateTimeOffset.UtcNow, null));

    public static SkillRequest IntentRequest(string name, params (string Name, string? Value)[] slots) =>
        Create(new Request("IntentRequest", DateTimeOffset.UtcNow,
            new Intent(name, slots.ToDictionary(slot => slot.Name, slot => new Slot(slot.Value, null)))));

    /// <summary>
    /// The same request arriving mid-conversation instead of as a one-shot.
    /// </summary>
    public static SkillRequest InConversation(this SkillRequest request) =>
        request with { Session = request.Session! with { New = false } };

    /// <summary>
    /// The same intent after the user answered Alexa's confirmation prompt, with CONFIRMED or DENIED.
    /// </summary>
    public static SkillRequest Answered(this SkillRequest request, string confirmationStatus) =>
        request with { Request = request.Request with { Intent = request.Request.Intent! with { ConfirmationStatus = confirmationStatus } } };

    /// <summary>
    /// A launch request as Alexa sends it over HTTPS.
    /// </summary>
    public static byte[] LaunchRequestJson(string applicationId, DateTimeOffset timestamp) => Encoding.UTF8.GetBytes($$"""
        {
          "version": "1.0",
          "session": {
            "new": true,
            "sessionId": "amzn1.echo-api.session.test",
            "application": { "applicationId": "{{applicationId}}" },
            "user": { "userId": "{{UserId}}" }
          },
          "context": {
            "System": {
              "application": { "applicationId": "{{applicationId}}" },
              "user": { "userId": "{{UserId}}" },
              "device": { "deviceId": "amzn1.ask.device.test", "supportedInterfaces": {} },
              "apiEndpoint": "https://api.amazonalexa.com"
            }
          },
          "request": {
            "type": "LaunchRequest",
            "requestId": "amzn1.echo-api.request.test",
            "timestamp": "{{timestamp.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}",
            "locale": "en-US"
          }
        }
        """);

    private static SkillRequest Create(Request request) =>
        new(new Session(true, new Application(SkillId), new User(UserId)), null, request);
}
