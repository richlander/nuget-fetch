using System.IO.Compression;
using NuGet.Versioning;

namespace NuGetFetch;

/// <summary>
/// Lean NuGet V3 metadata service for data that is awkward to get without
/// pulling in NuGet.Protocol's full resource/provider model.
/// </summary>
public class PackageMetadataService(HttpClient client)
{
    private const string RegistrationBaseUrl = "https://api.nuget.org/v3/registration5-semver1";
    private const string FlatContainerBaseUrl = "https://api.nuget.org/v3-flatcontainer";
    private const string VulnerabilityIndexUrl = "https://api.nuget.org/v3/vulnerabilities/index.json";

    /// <summary>
    /// Fetches a version-specific registration leaf for a package.
    /// </summary>
    public async Task<RegistrationLeaf?> GetRegistrationLeafAsync(string packageId, string version)
    {
        string id = packageId.ToLowerInvariant();
        string ver = version.ToLowerInvariant();
        using Stream stream = await GetDecodedStreamAsync($"{RegistrationBaseUrl}/{id}/{ver}.json");
        return await NuGetApi.GetRegistrationLeafAsync(stream);
    }

    /// <summary>
    /// Fetches catalog details from the catalog entry URL found in a registration leaf.
    /// </summary>
    public async Task<CatalogPackageDetails?> GetCatalogPackageDetailsAsync(string catalogEntryUrl)
    {
        using Stream stream = await GetDecodedStreamAsync(catalogEntryUrl);
        return await NuGetApi.GetCatalogPackageDetailsAsync(stream);
    }

    /// <summary>
    /// Fetches vulnerability ranges affecting a specific package version.
    /// </summary>
    public async Task<IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesAsync(string packageId, string version)
    {
        if (!NuGetVersion.TryParse(version, out NuGetVersion? packageVersion))
        {
            return [];
        }

        using Stream indexStream = await GetDecodedStreamAsync(VulnerabilityIndexUrl);
        IReadOnlyList<VulnerabilityIndexEntry>? index = await NuGetApi.GetVulnerabilityIndexAsync(indexStream);
        if (index is not { Count: > 0 })
        {
            return [];
        }

        string normalizedId = packageId.ToLowerInvariant();
        List<PackageVulnerability> result = [];

        foreach (VulnerabilityIndexEntry entry in index)
        {
            using Stream pageStream = await GetDecodedStreamAsync(entry.Id);
            Dictionary<string, IList<PackageVulnerability>>? page = await NuGetApi.GetVulnerabilityPageAsync(pageStream);
            if (page == null || !page.TryGetValue(normalizedId, out IList<PackageVulnerability>? vulnerabilities))
            {
                continue;
            }

            foreach (PackageVulnerability vulnerability in vulnerabilities)
            {
                if (VersionRange.TryParse(vulnerability.Versions, out VersionRange? range)
                    && range.Satisfies(packageVersion))
                {
                    result.Add(vulnerability);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Fetches aggregate metadata from registration, catalog, search, vulnerability, and flat-container HEAD endpoints.
    /// </summary>
    public async Task<PackageMetadata?> GetPackageMetadataAsync(string packageId, string version, bool includeVulnerabilities = true)
    {
        RegistrationLeaf? registration = await GetRegistrationLeafAsync(packageId, version);
        if (registration == null)
        {
            return null;
        }

        CatalogPackageDetails? catalog = registration.CatalogEntry == null
            ? null
            : await GetCatalogPackageDetailsAsync(registration.CatalogEntry);

        SearchResult? searchResult = await GetSearchResultAsync(packageId);
        long? versionDownloads = searchResult?.Versions?
            .FirstOrDefault(v => string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase))
            ?.Downloads;

        IReadOnlyList<PackageVulnerability>? vulnerabilities = includeVulnerabilities
            ? await GetVulnerabilitiesAsync(packageId, version)
            : null;

        long? packageSize = await GetPackageSizeAsync(packageId, version);

        return new PackageMetadata(
            Id: packageId,
            Version: version,
            Published: registration.Published,
            Listed: registration.Listed,
            PackageContentUrl: registration.PackageContent,
            CatalogEntryUrl: registration.CatalogEntry,
            Authors: catalog?.Authors,
            Description: catalog?.Description ?? searchResult?.Description,
            LicenseExpression: catalog?.LicenseExpression,
            ProjectUrl: catalog?.ProjectUrl,
            TotalDownloads: searchResult?.TotalDownloads,
            VersionDownloads: versionDownloads,
            VersionCount: searchResult?.Versions?.Count,
            Verified: searchResult?.Verified,
            Owners: null,
            PackageSize: packageSize,
            Deprecation: catalog?.Deprecation,
            Vulnerabilities: vulnerabilities is { Count: > 0 } ? vulnerabilities.ToList() : null);
    }

    /// <summary>
    /// Gets the .nupkg content length from the flat-container endpoint.
    /// </summary>
    public async Task<long?> GetPackageSizeAsync(string packageId, string version)
    {
        string id = packageId.ToLowerInvariant();
        string ver = version.ToLowerInvariant();
        using HttpRequestMessage request = new(HttpMethod.Head, $"{FlatContainerBaseUrl}/{id}/{ver}/{id}.{ver}.nupkg");
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
    }

    private async Task<SearchResult?> GetSearchResultAsync(string packageId)
    {
        string url = $"{NuGetClient.NuGetOrgSearchUrl}?q=packageid:{Uri.EscapeDataString(packageId)}&take=1";
        using Stream stream = await GetDecodedStreamAsync(url);
        SearchResponse? response = await NuGetApi.GetSearchResponseAsync(stream);
        return response?.Data.FirstOrDefault();
    }

    private async Task<Stream> GetDecodedStreamAsync(string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        bool isGzip = response.Content.Headers.ContentEncoding.Any(e => e.Equals("gzip", StringComparison.OrdinalIgnoreCase))
            || bytes is [0x1f, 0x8b, ..];

        if (!isGzip)
        {
            return new MemoryStream(bytes);
        }

        using var compressed = new MemoryStream(bytes);
        var decompressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
        {
            await gzip.CopyToAsync(decompressed);
        }

        decompressed.Position = 0;
        return decompressed;
    }
}
