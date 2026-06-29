---
name: nugetfetch
description: >-
  Lightweight, AOT-friendly NuGet client library for .NET: query versions, download and
  extract .nupkg packages, and search nuget.org. It is NOT the official NuGet.Protocol /
  NuGet.Client API — the surface is much smaller and the shapes differ. Key rules: you
  bring your own HttpClient (NuGetClient has no parameterless ctor), extraction lives on a
  separate static PackageExtractor class, and NormalizeVersion is static. See the body.
---

# NuGetFetch — download/extract/query NuGet packages from .NET

A small NuGet client. Package `NuGetFetch`, namespace `NuGetFetch`. AOT-compatible
(System.Text.Json source-gen, no reflection). Do NOT reach for the official
`NuGet.Protocol`/`NuGet.Client` types (`SourceRepository`, `FindPackageByIdResource`,
`PackageMetadataResource`, …) — none of those exist here.

## Core pattern (bring your own HttpClient)

```csharp
using NuGetFetch;

HttpClient http = new();             // YOU own it. NuGetClient has no parameterless ctor.
NuGetClient client = new(http);      // pass the HttpClient in.

string? latest = await client.GetLatestVersionAsync("Newtonsoft.Json");          // "13.0.4" or null
IReadOnlyList<string> all = await client.GetVersionsAsync("Newtonsoft.Json");    // ascending; all[0] is oldest
string? v = await client.ResolveVersionPatternAsync("Newtonsoft.Json", "12.0.*"); // "12.0.3"

await client.DownloadToFileAsync("Newtonsoft.Json", "13.0.3", "/tmp/n.nupkg");   // write .nupkg to disk
using Stream s = await client.DownloadAsync("Newtonsoft.Json", "13.0.3");        // or get a stream
```

## Extraction is a SEPARATE static class (not a NuGetClient method)

```csharp
string dir = PackageExtractor.Extract("/tmp/n.nupkg", "/tmp/out");        // from a file
string dir2 = await PackageExtractor.ExtractAsync(stream, "/tmp/out");    // from a DownloadAsync stream
bool ok = PackageExtractor.IsValidPackage(dir);
bool hasDlls = PackageExtractor.HasManagedLibraries(dir);
PackageIdentity? id = PackageExtractor.ParsePackageReference("Newtonsoft.Json@13.0.1"); // .Id, .Version
```

## Search is a SEPARATE class

```csharp
SearchService search = new(http);
IReadOnlyList<SearchResult> r = await search.SearchByPrefixAsync("Newtonsoft", take: 5); // r[i].Id
IReadOnlyList<SearchResult> q = await search.SearchAsync("json", take: 20, prerelease: false);
```

## Gotchas (where intuition is wrong)

- **No internal HttpClient.** `new NuGetClient()` does not exist; always `new NuGetClient(http)`.
  Same for `SearchService(http)`. Reuse one `HttpClient`.
- **`NuGetClient.NormalizeVersion(v)` is STATIC** (`"1.0.0.0"` -> `"1.0.0"`), not an instance method.
- **Extraction/parse helpers are on `PackageExtractor` (static)**, not on `NuGetClient`.
- **`GetVersionsAsync` is already in ascending NuGet version order** — oldest is `list[0]`,
  newest is `list[^1]`. Do NOT re-sort the list yourself: a naive string/`OrderBy` sort is
  wrong (`"10.0.1"` sorts before `"3.5.8"` lexically). Trust the returned order; use
  `GetLatestVersionAsync` for "latest stable".
- **Async, Task-returning, `*Async` suffix** on every network call; nullable returns where a
  package/version may not exist (`GetLatestVersionAsync`, `ResolveVersionPatternAsync`).

## After it builds: show your work

Show the final program and name the NuGetFetch calls used (e.g. `GetLatestVersionAsync`,
`PackageExtractor.IsValidPackage`). This library shadows the official NuGet client, so
surfacing the calls proves you used NuGetFetch — not a hallucinated `NuGet.Protocol` shape.
