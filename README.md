# NuGetFetch

[![NuGet](https://img.shields.io/nuget/v/NuGetFetch)](https://www.nuget.org/packages/NuGetFetch)

A lightweight NuGet client library for downloading and extracting
NuGet packages. Designed for AOT compatibility with zero
HttpClient ownership — bring your own.

```shell
dotnet add package NuGetFetch
```

## Features

- **Stream-based** — All JSON from streams, no string buffering
- **AOT compatible** — STJ source generation, no reflection
- **No HttpClient** — Accepts `HttpClient` from the caller
- **Two-tier caching** — Reads `~/.nuget/packages`, writes app cache
- **Source resolution** — Parses `nuget.config` files (with credentials)
- **TFM resolution** — Selects highest-priority target framework
- **Version resolution** — Full ascending list, latest, wildcard patterns, prerelease
- **Search** — Query nuget.org with prefix filtering

## Usage

```csharp
using NuGetFetch;

HttpClient httpClient = new();
NuGetClient client = new(httpClient);

// Get latest version
string? version = await client.GetLatestVersionAsync("Newtonsoft.Json");

// List every published version, ascending (oldest first, newest last)
IReadOnlyList<string> versions = await client.GetVersionsAsync("Newtonsoft.Json");
string oldest = versions[0];
string newest = versions[^1];

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

Two-tier cache: reads from `~/.nuget/packages` and writes
to an app-specific cache.

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

### Tool wrapper packages

.NET tools published as runtime-specific (e.g. NativeAOT) executables ship a thin wrapper
package whose only payload is a `DotnetToolSettings.xml` manifest pointing at per-RID packages
(`<id>.win-x64`, `<id>.osx-arm64`, `<id>.any`, …). To read managed metadata, redirect to the
portable `any` payload:

```csharp
string extractPath = PackageExtractor.Extract(nupkgPath, destination);

// Returns "mytool.any" when the package is a wrapper with no managed libraries; else null.
string? payloadId = PackageExtractor.TryGetToolWrapperRedirect(extractPath);
if (payloadId is not null)
{
    // Fetch + extract payloadId at the same version, then inspect that.
}
```

## API Overview

| Class              | Kind     | Purpose                      |
| ------------------ | -------- | ---------------------------- |
| `NuGetClient`      | Instance | Versions, download           |
| `SearchService`    | Instance | Search and prefix search     |
| `PackageCache`     | Instance | Two-tier package cache       |
| `ResponseCache`    | Instance | Disk cache with TTL          |
| `PackageExtractor` | Static   | Extract `.nupkg`, parse      |
| `SourceResolver`   | Static   | Parse `nuget.config` files   |
| `TfmResolver`      | Static   | Select best TFM              |
| `NuGetApi`         | Static   | Stream JSON deserialization  |

## Design

Follows the [distroessed][1] library patterns:

[1]: https://github.com/richlander/distroessed

- **POCOs** — Records with primary constructors for all data models
- **STJ source generation** — `NuGetJsonContext` for AOT-safe JSON
- **Stream-based helpers** — `NuGetApi` for stream deserialization
- **No HttpClient ownership** — Library never creates an `HttpClient`

## Dependencies

- [`NuGet.Versioning`](https://www.nuget.org/packages/NuGet.Versioning)
  — Version parsing and comparison
