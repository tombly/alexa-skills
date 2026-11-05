using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AlexaSkills.Alexa;

namespace AlexaSkills.Tests.TestSupport;

/// <summary>
/// Plays Alexa's part in request signing: a throwaway root CA, a signing certificate issued by it,
/// and a fake download of the certificate chain.
/// </summary>
sealed class FakeAlexaSigner : IDisposable
{
    public const string CertChainUrl = "https://s3.amazonaws.com/echo.api/echo-api-cert-test.pem";

    private readonly RSA _rootKey = RSA.Create(2048);
    private readonly RSA _signingKey = RSA.Create(2048);
    private readonly X509Certificate2 _root;
    private readonly string _chainPem;

    public FakeAlexaSigner(string dnsName = "echo-api.amazon.com", TimeSpan? expiresIn = null)
    {
        var now = DateTimeOffset.UtcNow;

        var rootRequest = new CertificateRequest("CN=Fake Alexa Root", _rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, critical: true));
        _root = rootRequest.CreateSelfSigned(now.AddDays(-30), now.AddDays(30));

        var signingRequest = new CertificateRequest($"CN={dnsName}", _signingKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName(dnsName);
        signingRequest.CertificateExtensions.Add(subjectAlternativeNames.Build());
        using var signing = signingRequest.Create(_root, now.AddDays(-7), now + (expiresIn ?? TimeSpan.FromDays(7)), RandomNumberGenerator.GetBytes(8));

        _chainPem = signing.ExportCertificatePem() + "\n" + _root.ExportCertificatePem();
    }

    public int Downloads { get; private set; }

    public AlexaRequestVerifier CreateVerifier(TimeProvider timeProvider, bool trustRoot = true) =>
        new(new StubHttpClientFactory(Download), timeProvider)
        {
            TrustedRoots = trustRoot ? new X509Certificate2Collection(_root) : null,
        };

    public string Sign(byte[] body) =>
        Convert.ToBase64String(_signingKey.SignData(body, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    public void Dispose()
    {
        _root.Dispose();
        _rootKey.Dispose();
        _signingKey.Dispose();
    }

    private string Download()
    {
        Downloads++;
        return _chainPem;
    }

    private sealed class StubHttpClientFactory(Func<string> download) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(download));
    }

    private sealed class StubHandler(Func<string> download) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(download()) });
    }
}
