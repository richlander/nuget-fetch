namespace NuGetFetch;

/// <summary>
/// Searches the NuGet Search API for packages by keyword or prefix.
/// </summary>
public class SearchService(HttpClient client)
{
    private const string DefaultSearchUrl = "https://azuresearch-usnc.nuget.org/query";

    /// <summary>
    /// Searches NuGet for packages matching the given query.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int take = 20,
        bool prerelease = false)
    {
        string pre = prerelease ? "true" : "false";
        string url = $"{DefaultSearchUrl}?q={Uri.EscapeDataString(query)}&take={take}&prerelease={pre}";

        using Stream stream = await client.GetStreamAsync(url);
        SearchResponse? response = await NuGetApi.GetSearchResponseAsync(stream);
        return (IReadOnlyList<SearchResult>?)response?.Data ?? [];
    }

    /// <summary>
    /// Searches NuGet for packages whose ID starts with the given prefix.
    /// Filters client-side since the search API doesn't support true prefix matching.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchByPrefixAsync(
        string prefix,
        int take = 100,
        bool prerelease = false)
    {
        IReadOnlyList<SearchResult> results = await SearchAsync(prefix, take, prerelease);

        return results
            .Where(r => r.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
