using System.Net.Http.Headers;
using NuGet.Versioning;

namespace NuGetFetch;

/// <summary>
/// Lightweight NuGet V3 API client. Accepts an HttpClient from the caller.
/// </summary>
public class NuGetClient(HttpClient client)
{
    internal const string NuGetOrgFlatContainer = "https://api.nuget.org/v3-flatcontainer/";
    internal const string NuGetOrgServiceIndex = "https://api.nuget.org/v3/index.json";
    internal const string NuGetOrgSearchUrl = "https://azuresearch-usnc.nuget.org/query";

    /// <summary>
    /// Gets all available versions for a package from a NuGet source.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetVersionsAsync(string packageId, string? sourceUrl = null)
    {
        string baseAddress = await ResolveBaseAddressAsync(sourceUrl);
        string url = $"{baseAddress}{packageId.ToLowerInvariant()}/index.json";
        using Stream stream = await GetStreamAsync(url);
        VersionIndex? index = await NuGetApi.GetVersionIndexAsync(stream);
        return (IReadOnlyList<string>?)index?.Versions ?? [];
    }

    /// <summary>
    /// Gets the latest version for a package. Uses the search API for nuget.org (faster).
    /// </summary>
    public async Task<string?> GetLatestVersionAsync(string packageId, bool includePrerelease = false, string? sourceUrl = null)
    {
        // For nuget.org, use the search API (faster than listing all versions)
        if (sourceUrl is null || IsNuGetOrg(sourceUrl))
        {
            return await GetLatestVersionFromSearchAsync(packageId, includePrerelease);
        }

        // For other sources, list all versions and pick the latest
        IReadOnlyList<string> versions = await GetVersionsAsync(packageId, sourceUrl);
        return FindLatestVersion(versions, includePrerelease);
    }

    /// <summary>
    /// Gets the latest version across multiple sources. Returns the first match.
    /// </summary>
    public async Task<string?> GetLatestVersionAsync(string packageId, IEnumerable<PackageSource> sources, bool includePrerelease = false)
    {
        foreach (PackageSource source in sources)
        {
            try
            {
                string? version = await GetLatestVersionAsync(packageId, includePrerelease, source.Url);
                if (version is not null)
                {
                    return version;
                }
            }
            catch (HttpRequestException)
            {
                // Try next source
            }
        }

        return null;
    }

    /// <summary>
    /// Downloads a package as a stream.
    /// </summary>
    public async Task<Stream> DownloadAsync(string packageId, string version, string? sourceUrl = null, PackageSourceCredential? credential = null)
    {
        string baseAddress = await ResolveBaseAddressAsync(sourceUrl);
        string id = packageId.ToLowerInvariant();
        string ver = version.ToLowerInvariant();
        string url = $"{baseAddress}{id}/{ver}/{id}.{ver}.nupkg";

        HttpRequestMessage request = new(HttpMethod.Get, url);

        if (credential is not null)
        {
            string encoded = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{credential.Username}:{credential.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    /// <summary>
    /// Downloads a package to a file.
    /// </summary>
    public async Task DownloadToFileAsync(string packageId, string version, string destinationPath, string? sourceUrl = null, PackageSourceCredential? credential = null)
    {
        using Stream source = await DownloadAsync(packageId, version, sourceUrl, credential);
        using FileStream dest = File.Create(destinationPath);
        await source.CopyToAsync(dest);
    }

    /// <summary>
    /// Resolves the PackageBaseAddress endpoint from a V3 service index.
    /// </summary>
    public async Task<string?> GetPackageBaseAddressAsync(string serviceIndexUrl)
    {
        using Stream stream = await GetStreamAsync(serviceIndexUrl);
        ServiceIndex? index = await NuGetApi.GetServiceIndexAsync(stream);

        string? baseAddress = index?.Resources
            .Where(r => r.Type.StartsWith("PackageBaseAddress", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Id)
            .FirstOrDefault();

        return baseAddress?.EndsWith('/') == true ? baseAddress : baseAddress + "/";
    }

    /// <summary>
    /// Resolves versions matching a wildcard pattern (e.g., "11.0.0-preview*").
    /// </summary>
    public async Task<string?> ResolveVersionPatternAsync(string packageId, string pattern, string? sourceUrl = null)
    {
        IReadOnlyList<string> versions = await GetVersionsAsync(packageId, sourceUrl);

        string prefix = pattern.TrimEnd('*');
        NuGetVersion? best = null;

        foreach (string v in versions)
        {
            if (v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                NuGetVersion.TryParse(v, out NuGetVersion? parsed))
            {
                if (best is null || parsed > best)
                {
                    best = parsed;
                }
            }
        }

        return best?.OriginalVersion;
    }

    private async Task<string?> GetLatestVersionFromSearchAsync(string packageId, bool includePrerelease)
    {
        string prerelease = includePrerelease ? "true" : "false";
        string url = $"{NuGetOrgSearchUrl}?q=packageid:{packageId}&take=1&prerelease={prerelease}";
        using Stream stream = await GetStreamAsync(url);
        SearchResponse? response = await NuGetApi.GetSearchResponseAsync(stream);
        return response?.Data.FirstOrDefault()?.Version;
    }

    private async Task<string> ResolveBaseAddressAsync(string? sourceUrl)
    {
        if (sourceUrl is null || IsNuGetOrg(sourceUrl))
        {
            return NuGetOrgFlatContainer;
        }

        return await GetPackageBaseAddressAsync(sourceUrl)
            ?? throw new InvalidOperationException($"Could not resolve PackageBaseAddress from {sourceUrl}");
    }

    private Task<Stream> GetStreamAsync(string url) =>
        client.GetStreamAsync(url);

    private static bool IsNuGetOrg(string url) =>
        url.Contains("api.nuget.org", StringComparison.OrdinalIgnoreCase);

    internal static string? FindLatestVersion(IReadOnlyList<string> versions, bool includePrerelease)
    {
        NuGetVersion? best = null;

        foreach (string v in versions)
        {
            if (NuGetVersion.TryParse(v, out NuGetVersion? parsed))
            {
                if (!includePrerelease && parsed.IsPrerelease)
                {
                    continue;
                }

                if (best is null || parsed > best)
                {
                    best = parsed;
                }
            }
        }

        return best?.OriginalVersion;
    }
}
