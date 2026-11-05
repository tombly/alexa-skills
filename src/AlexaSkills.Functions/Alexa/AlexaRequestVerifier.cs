using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AlexaSkills.Alexa;

/// <summary>
/// Checks that a request was signed by Alexa. A public HTTPS endpoint has to do this itself, and
/// skill certification tests that it does:
/// https://developer.amazon.com/docs/alexa/custom-skills/host-a-custom-skill-as-a-web-service.html
/// </summary>
public sealed class AlexaRequestVerifier(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
{
    public const string HttpClientName = "AlexaSigningCertificates";
    public const string CertChainUrlHeader = "SignatureCertChainUrl";
    public const string SignatureHeader = "Signature-256";

    private const string SigningCertificateDnsName = "echo-api.amazon.com";
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    // Alexa signs with the same certificate for a long time, so each URL is downloaded and validated once.
    private readonly ConcurrentDictionary<string, X509Certificate2> _certificates = new();

    /// <summary>
    /// Roots to trust instead of the system store. Only tests set this.
    /// </summary>
    public X509Certificate2Collection? TrustedRoots { get; init; }

    public async Task<VerificationResult> VerifyAsync(string? certChainUrl, string? signature, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(certChainUrl) || string.IsNullOrEmpty(signature))
        {
            return VerificationResult.Fail("Signature headers are missing.");
        }

        if (!IsAlexaCertChainUrl(certChainUrl, out var url))
        {
            return VerificationResult.Fail("Signing certificate URL isn't an Alexa URL.");
        }

        var signatureBytes = new byte[signature.Length];
        if (!Convert.TryFromBase64String(signature, signatureBytes, out var signatureLength))
        {
            return VerificationResult.Fail("Signature isn't base64.");
        }

        var (certificate, problem) = await GetSigningCertificateAsync(url, cancellationToken);
        if (certificate is null)
        {
            return VerificationResult.Fail(problem);
        }

        using var publicKey = certificate.GetRSAPublicKey();
        if (publicKey is null || !publicKey.VerifyData(body.Span, signatureBytes.AsSpan(0, signatureLength), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            return VerificationResult.Fail("Signature doesn't match the request body.");
        }

        return VerificationResult.Valid;
    }

    /// <summary>
    /// The URL must be https://s3.amazonaws.com/echo.api/… on port 443. Uri has already removed
    /// dot segments and lowercased the scheme, as the docs require.
    /// </summary>
    public static bool IsAlexaCertChainUrl(string certChainUrl, [NotNullWhen(true)] out Uri? url) =>
        Uri.TryCreate(certChainUrl, UriKind.Absolute, out url)
        && url.Scheme == Uri.UriSchemeHttps
        && string.Equals(url.Host, "s3.amazonaws.com", StringComparison.OrdinalIgnoreCase)
        && url.Port == 443
        && url.AbsolutePath.StartsWith("/echo.api/", StringComparison.Ordinal);

    private async Task<(X509Certificate2? Certificate, string? Problem)> GetSigningCertificateAsync(Uri url, CancellationToken cancellationToken)
    {
        if (_certificates.TryGetValue(url.AbsoluteUri, out var cached) && IsCurrent(cached))
        {
            return (cached, null);
        }

        string pem;
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            pem = await client.GetStringAsync(url, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            return (null, $"Couldn't download the signing certificate: {ex.Message}");
        }

        // The file holds the signing certificate first, then the intermediates that chain it to a root.
        var bundle = new X509Certificate2Collection();
        try
        {
            bundle.ImportFromPem(pem);
        }
        catch (CryptographicException)
        {
            return (null, "Signing certificate isn't valid PEM.");
        }

        if (bundle.Count == 0)
        {
            return (null, "Signing certificate file is empty.");
        }

        var certificate = bundle[0];
        if (!IsCurrent(certificate))
        {
            return (null, "Signing certificate is expired or not yet valid.");
        }

        if (!HasSigningDnsName(certificate))
        {
            return (null, $"Signing certificate isn't for {SigningCertificateDnsName}.");
        }

        if (!ChainsToTrustedRoot(certificate, bundle))
        {
            return (null, "Signing certificate doesn't chain to a trusted root.");
        }

        _certificates[url.AbsoluteUri] = certificate;
        return (certificate, null);
    }

    private bool IsCurrent(X509Certificate2 certificate)
    {
        var now = timeProvider.GetUtcNow();
        return now >= certificate.NotBefore.ToUniversalTime() && now <= certificate.NotAfter.ToUniversalTime();
    }

    private static bool HasSigningDnsName(X509Certificate2 certificate) =>
        certificate.Extensions[SubjectAlternativeNameOid] is { } extension
        && new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical)
            .EnumerateDnsNames()
            .Contains(SigningCertificateDnsName, StringComparer.OrdinalIgnoreCase);

    private bool ChainsToTrustedRoot(X509Certificate2 certificate, X509Certificate2Collection bundle)
    {
        using var chain = new X509Chain();
        // Amazon's checklist doesn't require revocation checks, and they'd add a network call.
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = timeProvider.GetUtcNow().LocalDateTime;
        chain.ChainPolicy.ExtraStore.AddRange(bundle);
        if (TrustedRoots is not null)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(TrustedRoots);
        }
        return chain.Build(certificate);
    }
}

public readonly record struct VerificationResult(bool IsValid, string? Problem)
{
    public static VerificationResult Valid => new(true, null);

    public static VerificationResult Fail(string? problem) => new(false, problem);
}
