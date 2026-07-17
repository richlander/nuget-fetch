---
name: caching
version: 0.7.1
description: >-
  Use when a tool fetches packages or nuget.org responses repeatedly and should avoid re-downloading —
  reuse the shared ~/.nuget/packages cache and/or an app-specific cache. NuGetFetch provides a two-tier
  PackageCache (extracted packages) and a ResponseCache (JSON/bytes with TTL). These are NuGetFetch
  types, not NuGet.Protocol's HttpSourceCache/SourceCacheContext. Requires the base `nugetfetch` pattern.
---

# Caching — two-tier package cache + response cache

Trap this prevents: re-downloading a package every run, hand-rolling a temp-dir cache, or reaching for
`SourceCacheContext` / `HttpSourceCache` (not present). Two purpose-built caches:

## PackageCache — reuse extracted packages

Two tiers: reads the shared, read-only `~/.nuget/packages`, writes to an app-specific cache.

```csharp
PackageCache cache = new("my-app");

// Check both tiers (NuGet global cache first, then app cache):
string? dir = cache.TryGet("Newtonsoft.Json", "13.0.3");

if (dir is null)
{
    using Stream nupkg = await client.DownloadAsync("Newtonsoft.Json", "13.0.3");
    string tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    await PackageExtractor.ExtractAsync(nupkg, tmp);
    dir = cache.Cache("Newtonsoft.Json", "13.0.3", tmp); // atomic rename into the app cache; returns path
}
```

- `TryGet(id, version)` → extracted directory path, or `null` on a miss. Checks the NuGet global cache
  **first**, then the app cache — so packages already restored on the machine cost zero download.
- `Cache(id, version, sourcePath)` copies an extracted dir into the app cache via **atomic rename**
  (no partial-copy races); returns the cache path (or `null` on failure).
- `TryGetLatestCachedVersion(id, includePrerelease)` → newest version already cached across both tiers
  (offline "what do I have?").
- `GetCachePath(id, version)` → where it *would* live; `CachePath` / `NuGetCachePath` expose the roots.
- `new PackageCache(appName, skipNuGetCache: true)` ignores `~/.nuget/packages` (app cache only).

## ResponseCache — cache API responses with TTL

For raw nuget.org JSON/bytes (version lists, search) keyed by category + key, with optional age limits:

```csharp
ResponseCache rc = new("my-app");

string? json = rc.TryGet("versions", "Newtonsoft.Json");                 // any age
string? fresh = rc.TryGet("versions", "Newtonsoft.Json", TimeSpan.FromHours(1)); // only if < 1h old
rc.Set("versions", "Newtonsoft.Json", jsonPayload);

CacheInfo info = rc.GetCacheInfo();  // .Path, .SizeBytes, .FileCount
long removed = rc.Clear("versions"); // clear one category (or Clear() for all); returns bytes freed
```

- `TryGet` / `TryGetBytes` have a plain overload (any age) and a `TimeSpan maxAge` overload
  (miss if staler). `Set` / `SetBytes` write; `extension` defaults to `"json"`.
- `GetCacheInfo(category?)` reports size/count for a category or the whole cache.

## Guardrails

- `PackageCache.TryGet` returns extracted **directories**, not `.nupkg` files — cache after extraction.
- Rely on the NuGet-global-first lookup; don't re-implement a `~/.nuget/packages` probe yourself.
- Use the `maxAge` overload for freshness-sensitive data (latest version); the plain overload is
  fine for immutable data (a specific version's contents).
