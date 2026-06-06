using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGetFetch;

public static class NuGetApi
{
    public static async ValueTask<ServiceIndex?> GetServiceIndexAsync(Stream json)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.ServiceIndex);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async ValueTask<VersionIndex?> GetVersionIndexAsync(Stream json)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.VersionIndex);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async ValueTask<SearchResponse?> GetSearchResponseAsync(Stream json)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.SearchResponse);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async ValueTask<RegistrationLeaf?> GetRegistrationLeafAsync(Stream json)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.RegistrationLeaf);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async ValueTask<CatalogPackageDetails?> GetCatalogPackageDetailsAsync(Stream json)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.CatalogPackageDetails);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async ValueTask<IReadOnlyList<VulnerabilityIndexEntry>?> GetVulnerabilityIndexAsync(Stream json)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.IReadOnlyListVulnerabilityIndexEntry);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async ValueTask<Dictionary<string, IList<PackageVulnerability>>?> GetVulnerabilityPageAsync(Stream json)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.DictionaryStringIListPackageVulnerability);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServiceIndex))]
[JsonSerializable(typeof(VersionIndex))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(RegistrationLeaf))]
[JsonSerializable(typeof(CatalogPackageDetails))]
[JsonSerializable(typeof(IReadOnlyList<VulnerabilityIndexEntry>))]
[JsonSerializable(typeof(Dictionary<string, IList<PackageVulnerability>>))]
public partial class NuGetJsonContext : JsonSerializerContext
{
}
