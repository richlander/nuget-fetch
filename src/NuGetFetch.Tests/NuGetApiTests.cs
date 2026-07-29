using System.Text;
using NuGetFetch;
using Xunit;

namespace NuGetFetch.Tests;

/// <summary>
/// Tests for NuGetApi stream-based JSON deserialization.
/// Includes resilience tests ported from dotnet-inspect.
/// </summary>
public class NuGetApiTests
{
    [Fact]
    public async Task GetVersionIndexAsync_ValidJson()
    {
        string json = """{"versions":["1.0.0","2.0.0","3.0.0-preview.1"]}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetVersionIndexAsync(stream, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(3, result.Versions.Count);
        Assert.Equal("1.0.0", result.Versions[0]);
        Assert.Equal("3.0.0-preview.1", result.Versions[2]);
    }

    [Fact]
    public async Task GetVersionIndexAsync_EmptyVersions()
    {
        string json = """{"versions":[]}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetVersionIndexAsync(stream, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Empty(result.Versions);
    }

    [Fact]
    public async Task GetServiceIndexAsync_ValidJson()
    {
        string json = """
        {
            "version": "3.0.0",
            "resources": [
                {"@id": "https://api.nuget.org/v3-flatcontainer/", "@type": "PackageBaseAddress/3.0.0"}
            ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetServiceIndexAsync(stream, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result.Resources);
        Assert.Equal("PackageBaseAddress/3.0.0", result.Resources[0].Type);
    }

    [Fact]
    public async Task GetServiceIndexAsync_FindsPackageBaseAddress()
    {
        string json = """
        {
            "version": "3.0.0",
            "resources": [
                {"@id": "https://example.com/search", "@type": "SearchQueryService"},
                {"@id": "https://example.com/flatcontainer/", "@type": "PackageBaseAddress/3.0.0"},
                {"@id": "https://example.com/registration/", "@type": "RegistrationsBaseUrl"}
            ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetServiceIndexAsync(stream, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        var packageBase = result.Resources.FirstOrDefault(r => r.Type.StartsWith("PackageBaseAddress"));
        Assert.NotNull(packageBase);
        Assert.Equal("https://example.com/flatcontainer/", packageBase.Id);
    }

    // --- Search response deserialization (ported from dotnet-inspect NuGetSearchServiceTests) ---

    [Fact]
    public async Task GetSearchResponseAsync_FullPayload()
    {
        string json = """
        {
            "totalHits": 3,
            "data": [
                {
                    "id": "Azure.AI.OpenAI",
                    "version": "2.1.0",
                    "description": "Azure OpenAI client library",
                    "totalDownloads": 5000000,
                    "verified": true,
                    "versions": [
                        {"version": "2.0.0", "@id": ""},
                        {"version": "2.1.0", "@id": ""}
                    ]
                },
                {
                    "id": "Azure.AI.TextAnalytics",
                    "version": "5.3.0",
                    "description": "Azure Text Analytics client",
                    "totalDownloads": 2000000,
                    "verified": true,
                    "versions": []
                },
                {
                    "id": "Azure.AI.FormRecognizer",
                    "version": "4.1.0",
                    "description": "Azure Form Recognizer client",
                    "totalDownloads": 1000000,
                    "verified": false,
                    "versions": []
                }
            ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetSearchResponseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.Data.Count);

        Assert.Equal("Azure.AI.OpenAI", result.Data[0].Id);
        Assert.Equal("2.1.0", result.Data[0].Version);
        Assert.Equal("Azure OpenAI client library", result.Data[0].Description);
        Assert.Equal(5_000_000, result.Data[0].TotalDownloads);
        Assert.True(result.Data[0].Verified);
        Assert.Equal(2, result.Data[0].Versions!.Count);

        Assert.Equal("Azure.AI.TextAnalytics", result.Data[1].Id);
        Assert.False(result.Data[2].Verified);
    }

    [Fact]
    public async Task GetSearchResponseAsync_EmptyData()
    {
        string json = """{"totalHits":0,"data":[]}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetSearchResponseAsync(stream, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Empty(result.Data);
    }

    // --- Azure DevOps Artifacts sends totalHits as a JSON string, not a number ---
    //
    // Both payloads below are the real wire shape captured from an Azure DevOps
    // Artifacts feed, including the ADO-only "@context", "lastReopen", and "index"
    // members. Before the JsonNumberHandling annotation on SearchResponse.TotalHits,
    // System.Text.Json raised:
    //
    //   JsonException: The JSON value could not be converted to System.Int32.
    //                  Path: $.totalHits
    //
    // GetSearchResponseAsync catches that and returns null, and SearchAsync then
    // coalesces null to an empty list. So the parse failure never surfaced as an
    // error: an Azure DevOps search silently returned zero results, which is
    // indistinguishable from "no package matched".

    [Fact]
    public async Task GetSearchResponseAsync_TotalHitsAsString_EmptyData()
    {
        string json = """
        {
            "@context": {"@vocab": "http://schema.nuget.org/schema#"},
            "data": [],
            "lastReopen": "2026-07-29T01:31:47.7885829Z",
            "index": "PackageIndex",
            "totalHits": "0"
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetSearchResponseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalHits);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetSearchResponseAsync_TotalHitsAsString_WithData()
    {
        // Azure DevOps reports "0" even when it returns matches, so TotalHits is not a
        // usable count on that host. Data is the load-bearing member. This asserts the
        // results survive regardless of what the count claims.
        //
        // The element shape below mirrors a real Azure DevOps hit member for member,
        // including the empty "@id"/"registration"/"iconUrl" strings and the null
        // "projectUrl"/"summary"/"title" that Azure DevOps sends but nuget.org does
        // not. Package identifiers are sanitized. Note that "downloads" arrives as a
        // real JSON number, so totalHits is the only member needing string tolerance.
        string json = """
        {
            "@context": {"@vocab": "http://schema.nuget.org/schema#"},
            "data": [
                {
                    "@id": "",
                    "@type": "Package",
                    "id": "Contoso.Internal.Core",
                    "version": "9.0.0",
                    "description": "Contoso.Internal.Core",
                    "versions": [{"@id": "Contoso.Internal.Core", "downloads": 0, "version": "9.0.0"}],
                    "authors": [],
                    "iconUrl": "",
                    "licenseUrl": "",
                    "projectUrl": null,
                    "registration": "",
                    "summary": null,
                    "tags": [],
                    "title": null
                },
                {
                    "@id": "",
                    "@type": "Package",
                    "id": "Contoso.Internal.Auth",
                    "version": "13.4.0-preview.6",
                    "description": "Contoso.Internal.Auth",
                    "versions": [{"@id": "Contoso.Internal.Auth", "downloads": 0, "version": "13.4.0-preview.6"}],
                    "authors": [],
                    "iconUrl": "",
                    "licenseUrl": "",
                    "projectUrl": null,
                    "registration": "",
                    "summary": null,
                    "tags": [],
                    "title": null
                }
            ],
            "lastReopen": "2026-07-29T01:31:47.7885829Z",
            "index": "PackageIndex",
            "totalHits": "0"
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetSearchResponseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalHits);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("Contoso.Internal.Core", result.Data[0].Id);
        Assert.Equal("9.0.0", result.Data[0].Version);
        Assert.Equal("Contoso.Internal.Auth", result.Data[1].Id);
        Assert.Equal("13.4.0-preview.6", result.Data[1].Version);
    }

    [Fact]
    public async Task GetSearchResponseAsync_TotalHitsAsNumber_StillParses()
    {
        // Guards the other direction: nuget.org sends totalHits as a number, and
        // AllowReadingFromString must not cost us that.
        string json = """{"totalHits":1234,"data":[{"id":"Newtonsoft.Json","version":"13.0.3"}]}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetSearchResponseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1234, result.TotalHits);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetSearchResponseAsync_MissingOptionalFields()
    {
        string json = """
        {
            "data": [
                {
                    "id": "SomePackage",
                    "version": "1.0.0"
                }
            ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await NuGetApi.GetSearchResponseAsync(stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result.Data);
        Assert.Equal("SomePackage", result.Data[0].Id);
        Assert.Equal("1.0.0", result.Data[0].Version);
        Assert.Null(result.Data[0].Description);
        Assert.Equal(0, result.Data[0].TotalDownloads);
        Assert.False(result.Data[0].Verified);
    }

    [Fact]
    public async Task GetSearchResponseAsync_MalformedJson_ReturnsNull()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not json"));
        var result = await NuGetApi.GetSearchResponseAsync(stream, TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetVersionIndexAsync_MalformedJson_ReturnsNull()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{broken"));
        var result = await NuGetApi.GetVersionIndexAsync(stream, TestContext.Current.CancellationToken);
        Assert.Null(result);
    }
}
