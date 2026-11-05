using System.Text;
using AlexaSkills.Alexa;
using AlexaSkills.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace AlexaSkills.Tests;

public sealed class AlexaRequestVerifierTests : IDisposable
{
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"version":"1.0"}""");

    private readonly FakeAlexaSigner _signer = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    // The valid and invalid examples from Amazon's docs, plus a couple more.
    [Theory]
    [InlineData("https://s3.amazonaws.com/echo.api/echo-api-cert.pem", true)]
    [InlineData("https://s3.amazonaws.com:443/echo.api/echo-api-cert.pem", true)]
    [InlineData("https://s3.amazonaws.com/echo.api/../echo.api/echo-api-cert.pem", true)]
    [InlineData("HTTPS://S3.AMAZONAWS.COM/echo.api/echo-api-cert.pem", true)]
    [InlineData("http://s3.amazonaws.com/echo.api/echo-api-cert.pem", false)]
    [InlineData("https://notamazon.com/echo.api/echo-api-cert.pem", false)]
    [InlineData("https://s3.amazonaws.com/EcHo.aPi/echo-api-cert.pem", false)]
    [InlineData("https://s3.amazonaws.com/invalid.path/echo-api-cert.pem", false)]
    [InlineData("https://s3.amazonaws.com:563/echo.api/echo-api-cert.pem", false)]
    [InlineData("https://s3.amazonaws.com/echo.api/../elsewhere/echo-api-cert.pem", false)]
    [InlineData("not a url", false)]
    public void Accepts_only_Alexa_certificate_urls(string url, bool expected) =>
        Assert.Equal(expected, AlexaRequestVerifier.IsAlexaCertChainUrl(url, out _));

    [Fact]
    public async Task Accepts_a_correctly_signed_request()
    {
        var result = await Verify(_signer.CreateVerifier(_time), _signer.Sign(Body), Body);

        Assert.True(result.IsValid, result.Problem);
    }

    [Fact]
    public async Task Rejects_a_body_that_was_changed_after_signing()
    {
        var result = await Verify(_signer.CreateVerifier(_time), _signer.Sign(Body), Encoding.UTF8.GetBytes("""{"version":"2.0"}"""));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null, "c2lnbmF0dXJl")]
    [InlineData(FakeAlexaSigner.CertChainUrl, null)]
    [InlineData(FakeAlexaSigner.CertChainUrl, "not base64!")]
    [InlineData("https://example.com/echo.api/cert.pem", "c2lnbmF0dXJl")]
    public async Task Rejects_missing_or_malformed_headers(string? certChainUrl, string? signature)
    {
        var result = await _signer.CreateVerifier(_time).VerifyAsync(certChainUrl, signature, Body, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Rejects_a_certificate_for_another_domain()
    {
        using var signer = new FakeAlexaSigner(dnsName: "example.com");

        var result = await Verify(signer.CreateVerifier(_time), signer.Sign(Body), Body);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Rejects_an_expired_certificate()
    {
        using var signer = new FakeAlexaSigner(expiresIn: TimeSpan.FromDays(-1));

        var result = await Verify(signer.CreateVerifier(_time), signer.Sign(Body), Body);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Rejects_a_certificate_that_does_not_chain_to_a_trusted_root()
    {
        var result = await Verify(_signer.CreateVerifier(_time, trustRoot: false), _signer.Sign(Body), Body);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Downloads_the_signing_certificate_once()
    {
        var verifier = _signer.CreateVerifier(_time);

        await Verify(verifier, _signer.Sign(Body), Body);
        await Verify(verifier, _signer.Sign(Body), Body);

        Assert.Equal(1, _signer.Downloads);
    }

    public void Dispose() => _signer.Dispose();

    private static Task<VerificationResult> Verify(AlexaRequestVerifier verifier, string signature, byte[] body) =>
        verifier.VerifyAsync(FakeAlexaSigner.CertChainUrl, signature, body, TestContext.Current.CancellationToken);
}
