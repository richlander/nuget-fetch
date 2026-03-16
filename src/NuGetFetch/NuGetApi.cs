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
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServiceIndex))]
[JsonSerializable(typeof(VersionIndex))]
[JsonSerializable(typeof(SearchResponse))]
public partial class NuGetJsonContext : JsonSerializerContext
{
}
