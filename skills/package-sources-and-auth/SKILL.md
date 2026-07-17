---
name: package-sources-and-auth
version: 0.7.1
description: >-
  Use when a fetch must go beyond nuget.org — discover and honor nuget.config sources, respect
  source precedence and <clear/>, or hit a private/authenticated feed with credentials. NuGetFetch's
  static SourceResolver parses nuget.config the way the official client does (it is NOT
  NuGet.Configuration / ISettings), and PackageSource/PackageSourceCredential carry the feed + auth.
  Requires the base `nugetfetch` pattern (BYO HttpClient).
---

# Package sources & auth — nuget.config, precedence, private feeds

Trap this prevents: hard-coding `https://api.nuget.org/...`, hand-parsing `nuget.config` XML, or
reaching for `NuGet.Configuration.Settings` (not present). `SourceResolver` (static) discovers and
merges config files; `PackageSource` / `PackageSourceCredential` carry a feed and its auth into the
`NuGetClient` calls.

## Discover configured sources

```csharp
// Walk up from the current directory, merge machine → user → project configs, in priority order:
IReadOnlyList<PackageSource> sources = SourceResolver.ResolveSources();

IReadOnlyList<string> configs = SourceResolver.FindConfigFiles();          // the NuGet.Config files found
IReadOnlyList<PackageSource> one = SourceResolver.LoadSourcesFromConfig(path); // sources from ONE config
```

`ResolveSources` matches official-client semantics: **most-distant config first** (machine → user →
project-level), and a **`<clear/>`** in a nearer config **drops all sources accumulated from parent
directories**. Disabled sources (`<disabledPackageSources>`) are excluded from the result.

## Use a source (and credentials) in a fetch

```csharp
var feed = new PackageSource("corp", "https://pkgs.corp.example/v3/index.json",
    new PackageSourceCredential("build", token));

IReadOnlyList<string> versions = await client.GetVersionsAsync(
    "Internal.Lib", feed.Url, feed.Credential);

string? latest = await client.GetLatestVersionAsync("Internal.Lib", sources, includePrerelease: false);
```

- `NuGetClient` methods take an optional `sourceUrl` + `PackageSourceCredential` — pass the feed's
  `Url` and `Credential`. Credentials become a Basic auth header (`PackageSource.GetAuthHeader()`).
- The `GetLatestVersionAsync(id, IEnumerable<PackageSource>, …)` overload probes the resolved sources
  in order and returns the first hit — the "search my configured feeds" workflow.
- `PackageSource.NuGetOrg` is the built-in default; `.IsNuGetOrg` and `.GetFlatContainerUrl()` help
  branch nuget.org vs a custom V3 feed.

## Non-nuget.org feeds resolve their own endpoints

For a custom `sourceUrl` (a V3 `index.json`), `NuGetClient` calls `GetPackageBaseAddressAsync` under
the hood to find the flat-container endpoint from the service index — you pass the `index.json` URL,
not a guessed flat-container URL.

## Guardrails

- Don't hand-read `nuget.config` — `SourceResolver` handles discovery, precedence, `<clear/>`, and
  disabled sources.
- `PackageSourceCredential.ToString()` masks the password; don't log the raw `Password`.
- Respect the returned order: `ResolveSources` is already in priority order — probe first-to-last.
