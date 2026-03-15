using System.Text.Json.Serialization;

namespace NuGetFetch;

// NuGet V3 Service Index

public record ServiceIndex(
    string Version,
    IList<ServiceResource> Resources);

public record ServiceResource(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("@type")] string Type,
    string? Comment = null);

// NuGet V3 Flat-Container Version Index

public record VersionIndex(
    IList<string> Versions);

// NuGet Search API

public record SearchResponse(
    int TotalHits,
    IList<SearchResult> Data);

public record SearchResult(
    string Id,
    string Version,
    IList<SearchVersion>? Versions = null);

public record SearchVersion(
    string Version,
    long Downloads);
