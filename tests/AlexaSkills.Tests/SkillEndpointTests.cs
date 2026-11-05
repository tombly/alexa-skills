using AlexaSkills.Alexa;
using AlexaSkills.Skills.MajorSystem;
using AlexaSkills.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AlexaSkills.Tests;

public sealed class SkillEndpointTests : IDisposable
{
    private readonly FakeAlexaSigner _signer = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly MajorSystemSkill _skill = new();

    [Fact]
    public async Task Answers_a_signed_request_for_its_skill()
    {
        var response = await Send(Endpoint(), Requests.LaunchRequestJson(Requests.SkillId, _time.GetUtcNow()));

        Assert.Equal("Major System, What can I help you with?", response?.Response.OutputSpeech?.Text);
    }

    [Fact]
    public async Task Rejects_a_request_for_another_skill()
    {
        var response = await Send(Endpoint(), Requests.LaunchRequestJson("amzn1.ask.skill.someone-else", _time.GetUtcNow()));

        Assert.Null(response);
    }

    [Fact]
    public async Task Rejects_a_request_more_than_150_seconds_old()
    {
        var response = await Send(Endpoint(), Requests.LaunchRequestJson(Requests.SkillId, _time.GetUtcNow().AddSeconds(-151)));

        Assert.Null(response);
    }

    [Fact]
    public async Task Rejects_an_unsigned_request()
    {
        var body = Requests.LaunchRequestJson(Requests.SkillId, _time.GetUtcNow());

        var response = await Endpoint().HandleAsync(_skill, body, null, null, TestContext.Current.CancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task Rejects_everything_until_the_skill_id_is_set()
    {
        var response = await Send(Endpoint(skillIds: []), Requests.LaunchRequestJson(Requests.SkillId, _time.GetUtcNow()));

        Assert.Null(response);
    }

    [Fact]
    public async Task Local_development_can_skip_the_checks()
    {
        var body = Requests.LaunchRequestJson("amzn1.ask.skill.anything", DateTimeOffset.UnixEpoch);

        var response = await Endpoint(verifyRequests: false, skillIds: []).HandleAsync(_skill, body, null, null, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
    }

    [Fact]
    public async Task Apologizes_when_the_skill_throws()
    {
        var skill = new ThrowingSkill();
        var body = Requests.LaunchRequestJson(Requests.SkillId, _time.GetUtcNow());

        var response = await Endpoint(skillIds: new() { [skill.Name] = Requests.SkillId })
            .HandleAsync(skill, body, FakeAlexaSigner.CertChainUrl, _signer.Sign(body), TestContext.Current.CancellationToken);

        Assert.Equal("Sorry, something went wrong. Please try again later.", response?.Response.OutputSpeech?.Text);
    }

    public void Dispose() => _signer.Dispose();

    private SkillEndpoint Endpoint(bool verifyRequests = true, Dictionary<string, string>? skillIds = null) => new(
        _signer.CreateVerifier(_time),
        Options.Create(new AlexaOptions
        {
            VerifyRequests = verifyRequests,
            SkillIds = skillIds ?? new() { [_skill.Name] = Requests.SkillId },
        }),
        _time,
        NullLogger<SkillEndpoint>.Instance);

    private Task<SkillResponse?> Send(SkillEndpoint endpoint, byte[] body) =>
        endpoint.HandleAsync(_skill, body, FakeAlexaSigner.CertChainUrl, _signer.Sign(body), TestContext.Current.CancellationToken);

    private sealed class ThrowingSkill : Skill
    {
        public override string Name => "Throwing";

        protected override Task<SkillResponse> OnLaunchAsync(SkillRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Storage is down.");

        protected override Task<SkillResponse> OnIntentAsync(SkillRequest request, Intent intent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Storage is down.");
    }
}
