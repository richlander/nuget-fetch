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
    string? Description = null,
    long TotalDownloads = 0,
    bool Verified = false,
    IList<SearchVersion>? Versions = null);

public record SearchVersion(
    string Version,
    long Downloads);

// NuGet V3 Registration API

public record RegistrationLeaf(
    DateTimeOffset? Published,
    bool Listed,
    string? PackageContent,
    string? CatalogEntry,
    string? Registration);

// NuGet V3 Catalog API

public record CatalogPackageDetails(
    string? Authors = null,
    string? Description = null,
    string? LicenseExpression = null,
    string? ProjectUrl = null,
    PackageDeprecation? Deprecation = null,
    IList<CatalogDependencyGroup>? DependencyGroups = null);

public record CatalogDependencyGroup(
    string? TargetFramework = null,
    IList<CatalogDependency>? Dependencies = null);

public record CatalogDependency(
    string Id,
    string? Range = null);

public record PackageDeprecation(
    IList<string>? Reasons = null,
    string? Message = null,
    AlternatePackage? AlternatePackage = null)
{
    public string? Summary => Message ?? (Reasons is { Count: > 0 } ? string.Join(", ", Reasons) : null);
}

public record AlternatePackage(
    string Id,
    string? Range = null);

// NuGet V3 Vulnerability API

public record VulnerabilityIndexEntry(
    [property: JsonPropertyName("@name")] string Name,
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("@updated")] DateTimeOffset? Updated = null,
    string? Comment = null);

public record PackageVulnerability(
    string Url,
    int Severity,
    string Versions);

// Aggregated lean metadata projection

public record PackageMetadata(
    string Id,
    string Version,
    DateTimeOffset? Published = null,
    bool? Listed = null,
    string? PackageContentUrl = null,
    string? CatalogEntryUrl = null,
    string? Authors = null,
    string? Description = null,
    string? LicenseExpression = null,
    string? ProjectUrl = null,
    long? TotalDownloads = null,
    long? VersionDownloads = null,
    int? VersionCount = null,
    bool? Verified = null,
    IList<string>? Owners = null,
    long? PackageSize = null,
    PackageDeprecation? Deprecation = null,
    IList<PackageVulnerability>? Vulnerabilities = null);
