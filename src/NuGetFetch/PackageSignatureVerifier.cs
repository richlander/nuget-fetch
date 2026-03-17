using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace NuGetFetch;

/// <summary>
/// Verifies NuGet package signatures using the embedded trusted root certificates.
/// Inspired by the NuGet client's signing verification (Apache 2.0 licensed).
/// </summary>
public static class PackageSignatureVerifier
{
    private const string SignatureFileName = ".signature.p7s";

    /// <summary>
    /// Verifies the signature of a .nupkg file on disk.
    /// </summary>
    public static SignatureVerificationResult VerifyPackage(string nupkgPath)
    {
        using FileStream stream = File.OpenRead(nupkgPath);
        return VerifyPackage(stream);
    }

    /// <summary>
    /// Verifies the signature of a .nupkg stream. The stream must be seekable.
    /// </summary>
    public static SignatureVerificationResult VerifyPackage(Stream nupkgStream)
    {
        byte[]? signatureBytes = ExtractSignature(nupkgStream);

        if (signatureBytes is null)
        {
            return new SignatureVerificationResult(SignatureStatus.Unsigned, Reason: null);
        }

        return VerifySignature(signatureBytes);
    }

    private static byte[]? ExtractSignature(Stream nupkgStream)
    {
        using ZipArchive archive = new(nupkgStream, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry? signatureEntry = archive.GetEntry(SignatureFileName);

        if (signatureEntry is null)
            return null;

        using Stream entryStream = signatureEntry.Open();
        using MemoryStream ms = new();
        entryStream.CopyTo(ms);
        return ms.ToArray();
    }

    private static SignatureVerificationResult VerifySignature(byte[] signatureBytes)
    {
        SignedCms signedCms = new();

        try
        {
            signedCms.Decode(signatureBytes);
        }
        catch (CryptographicException ex)
        {
            return new SignatureVerificationResult(
                SignatureStatus.Invalid,
                $"Failed to decode package signature: {ex.Message}");
        }

        // Verify CMS integrity (signature math is valid, no tampering)
        try
        {
            signedCms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException ex)
        {
            return new SignatureVerificationResult(
                SignatureStatus.Invalid,
                $"Package signature integrity check failed: {ex.Message}");
        }

        // Extract the signing certificate
        if (signedCms.SignerInfos.Count == 0)
        {
            return new SignatureVerificationResult(
                SignatureStatus.Invalid, "Package signature contains no signer information.");
        }

        X509Certificate2? signerCert = signedCms.SignerInfos[0].Certificate;
        if (signerCert is null)
        {
            return new SignatureVerificationResult(
                SignatureStatus.Invalid, "Could not extract signing certificate.");
        }

        // Build a certificate chain rooted in our trusted code-signing CAs
        SignatureVerificationResult chainResult = VerifyCertificateChain(
            signerCert, signedCms.Certificates, TrustedRoots.CodeSigningRoots);

        return chainResult;
    }

    private static SignatureVerificationResult VerifyCertificateChain(
        X509Certificate2 signerCert,
        X509Certificate2Collection extraCerts,
        X509Certificate2Collection trustedRoots)
    {
        using X509Chain chain = new();

        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);
        chain.ChainPolicy.ExtraStore.AddRange(extraCerts);

        // Disable revocation checking — matches NuGet SDK behavior for offline scenarios
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        bool isValid = chain.Build(signerCert);

        if (isValid)
        {
            return new SignatureVerificationResult(SignatureStatus.Valid, Reason: null);
        }

        // Collect chain status for diagnostics
        List<string> issues = new();
        foreach (X509ChainStatus status in chain.ChainStatus)
        {
            issues.Add($"{status.Status}: {status.StatusInformation}");
        }

        string reason = string.Join("; ", issues);
        return new SignatureVerificationResult(SignatureStatus.Invalid, reason);
    }
}

public enum SignatureStatus
{
    Valid,
    Unsigned,
    Invalid,
}

public record SignatureVerificationResult(SignatureStatus Status, string? Reason)
{
    public bool IsValid => Status == SignatureStatus.Valid;
    public bool IsUnsigned => Status == SignatureStatus.Unsigned;
}
