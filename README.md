# NuGetFetch

[![NuGet](https://img.shields.io/nuget/v/NuGetFetch)](https://www.nuget.org/packages/NuGetFetch)

A lightweight NuGet client library for downloading and extracting NuGet packages. Designed for AOT compatibility with zero HttpClient ownership — bring your own.

```
dotnet add package NuGetFetch
```

## Features

- **Stream-based** — All JSON deserialization from streams, no string buffering
- **AOT compatible** — STJ source generation, no reflection
- **No HttpClient** — Accepts `HttpClient` from the caller; doesn't create or manage one
- **Two-tier caching** — Reads from `~/.nuget/packages`, writes to app-specific cache
- **Source resolution** — Parses `nuget.config` files (with credentials)
- **TFM resolution** — Selects highest-priority target framework from extracted packages
- **Version resolution** — Latest version, wildcard patterns, prerelease support
- **Search** — Query nuget.org with prefix filtering

## Usage

```csharp
using NuGetFetch;

HttpClient httpClient = new();
NuGetClient client = new(httpClient);

// Get latest version
string? version = await client.GetLatestVersionAsync("Newtonsoft.Json");

// Download and extract
using Stream nupkg = await client.DownloadAsync("Newtonsoft.Json", version!);
string extractPath = Path.Combine(Path.GetTempPath(), "Newtonsoft.Json");
await PackageExtractor.ExtractAsync(nupkg, extractPath);

// Find the best TFM
string? tfmPath = TfmResolver.ResolvePackagePath(extractPath);
```

### Search

```csharp
SearchService search = new(httpClient);

// Search for packages
IReadOnlyList<SearchResult> results = await search.SearchAsync("json serializer");

// Prefix search
IReadOnlyList<SearchResult> results = await search.SearchByPrefixAsync("Newtonsoft");
```

### Caching

Two-tier cache: reads from the global NuGet cache (`~/.nuget/packages`) and writes to an app-specific cache.

```csharp
PackageCache cache = new("my-app");

// Check cache first
string? cached = cache.TryGet("Newtonsoft.Json", "13.0.3");

if (cached is null)
{
    // Download, extract, then cache
    using Stream nupkg = await client.DownloadAsync("Newtonsoft.Json", "13.0.3");
    string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    await PackageExtractor.ExtractAsync(nupkg, tempPath);
    cached = cache.Cache("Newtonsoft.Json", "13.0.3", tempPath);
}
```

### Source Resolution

```csharp
// Auto-discover from nuget.config files
IReadOnlyList<PackageSource> sources = SourceResolver.ResolveSources();

// Or specify explicitly
IReadOnlyList<PackageSource> sources = SourceResolver.ResolveSources(
    explicitSource: "https://my-feed.example.com/v3/index.json");
```

### Version Patterns

```csharp
// Resolve wildcard patterns
string? version = await client.ResolveVersionPatternAsync("Newtonsoft.Json", "13.*");

// Parse "Package@Version" specs
PackageIdentity? parsed = PackageExtractor.ParsePackageReference("Newtonsoft.Json@13.0.3");
```

## API Overview

| Class | Kind | Purpose |
|-------|------|---------|
| `NuGetClient` | Instance (takes `HttpClient`) | Versions, download, version patterns |
| `SearchService` | Instance (takes `HttpClient`) | Search and prefix search |
| `PackageCache` | Instance (takes `appName`) | Two-tier package cache |
| `ResponseCache` | Instance (takes `appName`) | Disk cache with TTL and categories |
| `PackageExtractor` | Static | Extract `.nupkg`, parse package specs |
| `SourceResolver` | Static | Discover and parse `nuget.config` files |
| `TfmResolver` | Static | Select best TFM from extracted packages |
| `NuGetApi` | Static | Stream-based JSON deserialization |

## Design

Follows the [distroessed](https://github.com/richlander/distroessed) library patterns:

- **POCOs** — Records with primary constructors for all data models
- **STJ source generation** — `NuGetJsonContext` for AOT-safe JSON serialization
- **Stream-based helpers** — `NuGetApi` static class for stream deserialization
- **No HttpClient ownership** — Library never creates an `HttpClient`

## Dependencies

- [`NuGet.Versioning`](https://www.nuget.org/packages/NuGet.Versioning) — Version parsing and comparison
