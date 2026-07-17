---
name: version-resolution
version: 0.7.0
description: >-
  Use when resolving WHICH version of a package to use — latest stable vs prerelease, a floating
  wildcard pattern, or normalizing a version string — with NuGetFetch. Covers the sharp edges the
  official-client intuition gets wrong: NuGetFetch wildcard patterns are PREFIX matches (not
  segment-aware globs), NormalizeVersion lowercases prerelease tags and drops build metadata, and
  the version list is already ascending. Requires the base `nugetfetch` pattern (BYO HttpClient).
---

# Version resolution — latest, floating, and normalization

Trivial lookups (`GetLatestVersionAsync`, `GetVersionsAsync`) are in the base skill. This skill is
the sharp edges: prerelease, wildcard patterns, and normalization — where the frontier mis-predicts
because it assumes SemVer-range semantics NuGetFetch does not implement.

## Latest: stable vs prerelease

```csharp
string? stable = await client.GetLatestVersionAsync("Serilog");                        // newest STABLE
string? withPre = await client.GetLatestVersionAsync("Serilog", includePrerelease: true); // may be -dev/-beta
```

`GetLatestVersionAsync` uses the search endpoint (fast) and filters prerelease by the flag — it does
NOT just take `GetVersionsAsync()[^1]` (that last entry can be a prerelease).

## Wildcard patterns are PREFIX matches — not segment globs

`ResolveVersionPatternAsync(id, pattern)` does `pattern.TrimEnd('*')` then `version.StartsWith(prefix)`,
returning the highest match. The `*` is a raw string-prefix cut, NOT a version-segment glob:

```csharp
await client.ResolveVersionPatternAsync("Newtonsoft.Json", "12.0.*");        // "12.0.3"  ✓ dot pins the segment
await client.ResolveVersionPatternAsync("Newtonsoft.Json", "11.0.0-preview*"); // highest 11.0.0-preview* tag
```

**The trap:** a pattern without a boundary dot prefix-matches across majors/minors —
- `"1*"`   → prefix `"1"`   → matches `1.x`, `10.x`, `11.x`, `12.x`, `13.x` (everything starting `1`).
- `"1.1*"` → prefix `"1.1"` → matches `1.1.x`, `1.10.x`, `1.11.x` (not just the 1.1 line).

Rule: **include the trailing dot to pin a segment boundary** (`"12.0.*"`, `"1.0.*"`). Returns `null`
if nothing matches.

## Normalize — canonical NuGet form (static, with quirks)

```csharp
NuGetClient.NormalizeVersion("1.0.0.0");    // "1.0.0"        (trailing .0 trimmed)
NuGetClient.NormalizeVersion("1.02.3");     // "1.2.3"        (leading zeros stripped)
NuGetClient.NormalizeVersion("1.0.0+build"); // "1.0.0"        (build metadata dropped)
NuGetClient.NormalizeVersion("1.0.0-Beta"); // "1.0.0-beta"   (prerelease tag LOWERCASED)
```

`NormalizeVersion` is **static on `NuGetClient`**; it parses with NuGet rules and returns
`ToNormalizedString().ToLowerInvariant()`, so **prerelease labels come back lowercased** and build
metadata is discarded. Unparseable input is returned lowercased as-is (no throw).

## Multi-source latest

```csharp
IReadOnlyList<PackageSource> sources = SourceResolver.ResolveSources();
string? v = await client.GetLatestVersionAsync("Internal.Lib", sources, includePrerelease: false);
```

The `sources` overload probes sources in order and returns the FIRST match. (See
**package-sources-and-auth** for building the source list and credentials.)

## Guardrails

- Don't re-sort `GetVersionsAsync` output — it's ascending; a string sort is wrong.
- Don't treat `ResolveVersionPatternAsync` patterns as NuGet ranges (`[1.0,2.0)`) or segment globs.
- Don't expect `NormalizeVersion` to preserve prerelease casing or build metadata.
