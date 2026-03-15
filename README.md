# NuGetFetch

A lightweight NuGet client library for downloading and extracting NuGet packages. Designed for AOT compatibility with zero HttpClient ownership — bring your own.

## Features

- **Stream-based** — All JSON deserialization from streams, no string buffering
- **AOT compatible** — STJ source generation, no reflection
- **No HttpClient** — Accepts `HttpClient` from the caller; doesn't create or manage one
- **Two-tier caching** — Reads from `~/.nuget/packages`, writes to app-specific cache
- **Source resolution** — Parses `nuget.config` files (with credentials)
- **TFM resolution** — Selects highest-priority target framework from extracted packages
- **Version resolution** — Latest version, wildcard patterns, prerelease support

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

### Caching

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

## Design

Follows the [distroessed](https://github.com/richlander/distroessed) library patterns:

- **POCOs** — Records with primary constructors for all data models
- **STJ source generation** — `NuGetJsonContext` for AOT-safe JSON serialization
- **Stream-based helpers** — `NuGetApi` static class for stream deserialization
- **No HttpClient ownership** — Library never creates an `HttpClient`

## Dependencies

- [`NuGet.Versioning`](https://www.nuget.org/packages/NuGet.Versioning) — Version parsing and comparison
