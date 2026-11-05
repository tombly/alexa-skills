using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlexaSkills.Alexa;

/// <summary>
/// Turns an HTTPS request from Alexa into a call to a skill, after checking that Alexa sent it,
/// recently, for that skill. Rejected requests get 400 Bad Request, as Alexa's docs ask.
/// </summary>
public sealed class SkillEndpoint(
    AlexaRequestVerifier verifier,
    IOptions<AlexaOptions> options,
    TimeProvider timeProvider,
    ILogger<SkillEndpoint> logger)
{
    private const int MaxBodyBytes = 128 * 1024;
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(150);

    public async Task<IActionResult> HandleAsync(HttpRequest httpRequest, Skill skill, CancellationToken cancellationToken)
    {
        if (httpRequest.ContentLength > MaxBodyBytes)
        {
            return new BadRequestResult();
        }

        using var body = new MemoryStream();
        await httpRequest.Body.CopyToAsync(body, cancellationToken);

        var response = await HandleAsync(
            skill,
            body.ToArray(),
            httpRequest.Headers[AlexaRequestVerifier.CertChainUrlHeader],
            httpRequest.Headers[AlexaRequestVerifier.SignatureHeader],
            cancellationToken);

        if (response is null)
        {
            return new BadRequestResult();
        }

        return new ContentResult
        {
            Content = JsonSerializer.Serialize(response, AlexaJson.Options),
            ContentType = "application/json",
            StatusCode = StatusCodes.Status200OK,
        };
    }

    /// <returns>The skill's response, or null if the request should be rejected.</returns>
    public async Task<SkillResponse?> HandleAsync(Skill skill, ReadOnlyMemory<byte> body, string? certChainUrl, string? signature, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (body.Length > MaxBodyBytes)
        {
            return Reject(skill, "Request body is too large.");
        }

        if (settings.VerifyRequests)
        {
            var verification = await verifier.VerifyAsync(certChainUrl, signature, body, cancellationToken);
            if (!verification.IsValid)
            {
                return Reject(skill, verification.Problem);
            }
        }

        SkillRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SkillRequest>(body.Span, AlexaJson.Options);
        }
        catch (JsonException)
        {
            return Reject(skill, "Request body isn't valid JSON.");
        }

        if (request?.Request is null)
        {
            return Reject(skill, "Request body isn't an Alexa request.");
        }

        // Stops someone replaying a request they captured.
        if (settings.VerifyRequests && (timeProvider.GetUtcNow() - request.Request.Timestamp).Duration() > TimestampTolerance)
        {
            return Reject(skill, "Request timestamp is more than 150 seconds off.");
        }

        if (!settings.SkillIds.TryGetValue(skill.Name, out var skillId) || string.IsNullOrEmpty(skillId))
        {
            if (settings.VerifyRequests)
            {
                return Reject(skill, $"Alexa:SkillIds:{skill.Name} isn't set.");
            }
        }
        else if (request.ApplicationId != skillId)
        {
            return Reject(skill, "Request is for a different skill.");
        }

        logger.LogInformation("{Skill} received {RequestType} {Intent}", skill.Name, request.Request.Type, request.Request.Intent?.Name);
        if (request.Request.Error is { } error)
        {
            // Alexa's only report of a problem on its side, such as a response it couldn't use.
            logger.LogWarning("{Skill} got an error from Alexa ({Reason}): {ErrorType} {ErrorMessage}", skill.Name, request.Request.Reason, error.Type, error.Message);
        }

        try
        {
            return await skill.HandleAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{Skill} failed on {RequestType} {Intent}", skill.Name, request.Request.Type, request.Request.Intent?.Name);
            return Reply.Tell("Sorry, something went wrong. Please try again later.");
        }
    }

    private SkillResponse? Reject(Skill skill, string? problem)
    {
        logger.LogWarning("Rejected a request to {Skill}: {Problem}", skill.Name, problem);
        return null;
    }
}
