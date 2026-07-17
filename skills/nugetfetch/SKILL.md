---
name: nugetfetch
version: 0.7.0
description: >-
  Use when a .NET program needs to query, download, extract, or search NuGet packages
  programmatically (a version lookup, a .nupkg download, a package inspection tool, a
  restore-like flow). NuGetFetch is a small AOT-friendly NuGet client — it is NOT the
  official NuGet.Protocol / NuGet.Client API (SourceRepository, FindPackageByIdResource,
  PackageMetadataResource do NOT exist here). Start here for the required shapes; branch to
  the domain skills for version resolution, TFM asset selection, sources/auth, signature
  verification, and caching.
---

# NuGetFetch — query / download / extract NuGet packages from .NET

Package `NuGetFetch`, namespace `NuGetFetch`. AOT-compatible (System.Text.Json source-gen,
no reflection). Reach for it whenever code would otherwise hand-write `HttpClient` calls to
the nuget.org REST endpoints.

**Do NOT reach for the official `NuGet.Protocol` / `NuGet.Client` types** (`SourceRepository`,
`FindPackageByIdResource`, `PackageMetadataResource`, `PackageArchiveReader`, …). None exist
here. The surface is smaller and the shapes differ — use the ones below.

## The core pattern (bring your own HttpClient)

```csharp
using NuGetFetch;

HttpClient http = new();            // YOU own it. NuGetClient has NO parameterless ctor.
NuGetClient client = new(http);     // pass the HttpClient in. Reuse one instance.

string? latest = await client.GetLatestVersionAsync("Newtonsoft.Json");        // "13.0.4" or null
IReadOnlyList<string> all = await client.GetVersionsAsync("Newtonsoft.Json");  // ASCENDING; all[0] oldest
using Stream nupkg = await client.DownloadAsync("Newtonsoft.Json", "13.0.3");  // or DownloadToFileAsync(id, ver, path)
```

## Extraction & search are SEPARATE classes (not NuGetClient methods)

```csharp
// Extraction: static PackageExtractor
string dir = await PackageExtractor.ExtractAsync(nupkg, "/tmp/out");   // from a DownloadAsync stream
string dir2 = PackageExtractor.Extract("/tmp/n.nupkg", "/tmp/out");    // from a file on disk
bool ok = PackageExtractor.IsValidPackage(dir);
bool hasDlls = PackageExtractor.HasManagedLibraries(dir);
PackageIdentity? id = PackageExtractor.ParsePackageReference("Newtonsoft.Json@13.0.1"); // .Id / .Version

// Search: SearchService
SearchService search = new(http);
IReadOnlyList<SearchResult> hits = await search.SearchByPrefixAsync("Newtonsoft", take: 5); // hits[i].Id
IReadOnlyList<SearchResult> q = await search.SearchAsync("json", take: 20, prerelease: false);
```

## Gotchas (where intuition is wrong)

- **No internal HttpClient.** `new NuGetClient()` / `new SearchService()` do not exist — always
  pass an `HttpClient`.
- **`GetVersionsAsync` is already ascending** — oldest is `list[0]`, newest is `list[^1]`. Do NOT
  re-sort: a naive string sort is wrong (`"10.0.1"` < `"3.5.8"` lexically). Use `GetLatestVersionAsync`
  for "latest stable."
- **`NuGetClient.NormalizeVersion(v)` is STATIC** (`"1.0.0.0"` → `"1.0.0"`), not an instance method.
- **Extraction / parse / validity helpers live on the static `PackageExtractor`**, not on `NuGetClient`.
  Parse `"id@version"` with `PackageExtractor.ParsePackageReference` — do NOT hand-split on `@`.
- **Every network call is `*Async`, Task-returning**; nullable returns where a package/version may
  not exist (`GetLatestVersionAsync`, `ResolveVersionPatternAsync`).

## After it builds: show your work

Show the final program and name the NuGetFetch calls used (e.g. `GetLatestVersionAsync`,
`PackageExtractor.IsValidPackage`). This library shadows the official NuGet client, so surfacing
the calls proves you used NuGetFetch — not a hallucinated `NuGet.Protocol` shape.

## Which skill for what

Pull the matching domain skill; don't hand-roll what the library owns:

- **version-resolution** — prerelease/floating latest, wildcard patterns (and the prefix-match trap),
  `NormalizeVersion` edge cases, multi-source latest.
- **tfm-asset-selection** — pick the right `lib/<tfm>` assembly for a target framework (`TfmResolver`).
- **package-sources-and-auth** — discover/parse `nuget.config`, source precedence + `<clear/>`, private
  feeds and credentials (`SourceResolver`, `PackageSource`).
- **signature-verification** — verify author vs repository signatures and trust (`PackageSignatureVerifier`).
- **caching** — two-tier package/response cache for repeated fetches (`PackageCache`, `ResponseCache`).
