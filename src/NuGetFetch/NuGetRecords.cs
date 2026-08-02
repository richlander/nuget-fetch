using System.Text.Json.Serialization;

namespace NuGetFetch;

// NuGet V3 Service Index

public record ServiceIndex(
    string Version,
    IReadOnlyList<ServiceResource> Resources);

public record ServiceResource(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("@type")] string Type,
    string? Comment = null);

// NuGet V3 Flat-Container Version Index

public record VersionIndex(
    IReadOnlyList<string> Versions);

// NuGet Search API

// Azure DevOps Artifacts serializes totalHits as a JSON string ("0") rather than a
// number, so reading it strictly as Int32 fails and takes the whole response down
// with it. nuget.org sends a number. Accept both.
public record SearchResponse(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int TotalHits,
    IReadOnlyList<SearchResult> Data);

public record SearchResult(
    string Id,
    string Version,
    string? Description = null,
    long TotalDownloads = 0,
    bool Verified = false,
    IReadOnlyList<SearchVersion>? Versions = null);

public record SearchVersion(
    string Version,
    long Downloads);
