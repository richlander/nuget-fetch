using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGetFetch;

public static class NuGetApi
{
    public static ValueTask<ServiceIndex?> GetServiceIndexAsync(Stream json) =>
        JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.ServiceIndex);

    public static ValueTask<VersionIndex?> GetVersionIndexAsync(Stream json) =>
        JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.VersionIndex);

    public static ValueTask<SearchResponse?> GetSearchResponseAsync(Stream json) =>
        JsonSerializer.DeserializeAsync(json, NuGetJsonContext.Default.SearchResponse);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServiceIndex))]
[JsonSerializable(typeof(VersionIndex))]
[JsonSerializable(typeof(SearchResponse))]
public partial class NuGetJsonContext : JsonSerializerContext
{
}
