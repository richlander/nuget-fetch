---
name: signature-verification
version: 0.7.0
description: >-
  Use when you must verify a NuGet package's signature — is a .nupkg validly signed, by the author or
  by the repository (nuget.org), and is it trusted? NuGetFetch's static PackageSignatureVerifier checks
  the embedded PKCS#7 signature against bundled trusted roots (it is NOT NuGet.Packaging.Signing /
  SignedPackageArchive). Requires the base `nugetfetch` pattern; operates on a .nupkg file or stream.
---

# Signature verification — author vs repository trust

Trap this prevents: shelling out to `nuget verify`, reaching for `NuGet.Packaging.Signing`
(`SignedPackageArchive`, `SignatureVerifier` — not present), or treating "unsigned" as "invalid."
`PackageSignatureVerifier` (static) parses the package's embedded signature and validates it against
NuGetFetch's bundled trusted roots.

## Verify a package

```csharp
SignatureVerificationResult r = PackageSignatureVerifier.VerifyPackage("/tmp/Serilog.3.1.1.nupkg");
// or from a seekable stream:
using Stream s = await client.DownloadAsync("Serilog", "3.1.1");
SignatureVerificationResult r2 = PackageSignatureVerifier.VerifyPackage(s);

Console.WriteLine(r.Status);         // Valid | Unsigned | Invalid
Console.WriteLine(r.SignatureType);  // Author | Repository
Console.WriteLine(r.Publisher);      // signing cert CN, e.g. the author or "NuGet.org"
```

## The three states — distinguish Unsigned from Invalid

`SignatureStatus` has **three** values, not a bool:

- `Valid` — signature present and verifies against a trusted root.
- `Unsigned` — no signature present. NOT a failure; check `r.IsUnsigned` and apply policy.
- `Invalid` — a signature is present but does not verify (tampered, untrusted root, or a
  legacy/unrecognized signature format). This is the hard failure.

```csharp
if (r.Status == SignatureStatus.Invalid) { /* reject */ }
else if (r.IsUnsigned) { /* policy decision: allow legacy unsigned? */ }
else { /* r.IsValid — trusted */ }
```

## Author vs repository, and the counter-signature

nuget.org packages are typically **author-signed and then repository counter-signed** by nuget.org.
The result surfaces both:

```csharp
r.SignatureType;      // primary: Author (or Repository)
r.CounterSignature;   // the repository (nuget.org) SignatureVerificationResult, if present
r.Timestamp;          // RFC 3161 trusted timestamp, if present
r.Thumbprint;         // SHA-256 of the signing cert
r.ContentHash;        // base64 hash the signer committed to
```

Use `SignatureType` to tell an author signature from a repository signature; a package signed only by
the author still carries a repository counter-signature when it came from nuget.org.

## Verify an extracted signature file

For a package already extracted to a cache, verify the raw `.signature.p7s` directly:

```csharp
SignatureVerificationResult r = PackageSignatureVerifier.VerifySignatureFile(sigPath);
```

## Guardrails

- The stream overload needs a **seekable** stream (the signature lives at the end of the ZIP).
- Treat `Unsigned` as a policy decision, not an error — only `Invalid` is a hard verification failure.
- Trust roots are bundled in NuGetFetch; you don't supply a cert store.
