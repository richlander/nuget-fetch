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
        var result = await NuGetApi.GetVersionIndexAsync(stream);
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
        var result = await NuGetApi.GetVersionIndexAsync(stream);
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
        var result = await NuGetApi.GetServiceIndexAsync(stream);
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
        var result = await NuGetApi.GetServiceIndexAsync(stream);
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
        var result = await NuGetApi.GetSearchResponseAsync(stream);

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
        var result = await NuGetApi.GetSearchResponseAsync(stream);
        Assert.NotNull(result);
        Assert.Empty(result.Data);
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
        var result = await NuGetApi.GetSearchResponseAsync(stream);

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
        var result = await NuGetApi.GetSearchResponseAsync(stream);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetVersionIndexAsync_MalformedJson_ReturnsNull()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{broken"));
        var result = await NuGetApi.GetVersionIndexAsync(stream);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRegistrationLeafAsync_ValidJson()
    {
        string json = """
        {
          "catalogEntry": "https://api.nuget.org/v3/catalog0/data/pkg.json",
          "listed": true,
          "packageContent": "https://api.nuget.org/v3-flatcontainer/package/1.0.0/package.1.0.0.nupkg",
          "published": "2024-01-02T03:04:05Z",
          "registration": "https://api.nuget.org/v3/registration5-semver1/package/index.json"
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        RegistrationLeaf? result = await NuGetApi.GetRegistrationLeafAsync(stream);

        Assert.NotNull(result);
        Assert.True(result.Listed);
        Assert.Equal("https://api.nuget.org/v3/catalog0/data/pkg.json", result.CatalogEntry);
        Assert.Equal(2024, result.Published!.Value.Year);
    }

    [Fact]
    public async Task GetCatalogPackageDetailsAsync_WithDeprecationAndDependencies()
    {
        string json = """
        {
          "authors": "Test Author",
          "description": "Test package",
          "licenseExpression": "MIT",
          "projectUrl": "https://example.test",
          "deprecation": {
            "reasons": [ "Legacy" ],
            "message": "Use something else",
            "alternatePackage": { "id": "Other.Package", "range": "[2.0.0, )" }
          },
          "dependencyGroups": [
            {
              "targetFramework": "net8.0",
              "dependencies": [
                { "id": "Dependency.A", "range": "[1.0.0, 2.0.0)" }
              ]
            }
          ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        CatalogPackageDetails? result = await NuGetApi.GetCatalogPackageDetailsAsync(stream);

        Assert.NotNull(result);
        Assert.Equal("Test Author", result.Authors);
        Assert.Equal("MIT", result.LicenseExpression);
        Assert.Equal("Use something else", result.Deprecation!.Message);
        Assert.Equal("Other.Package", result.Deprecation.AlternatePackage!.Id);
        Assert.Equal("Dependency.A", result.DependencyGroups![0].Dependencies![0].Id);
    }

    [Fact]
    public async Task GetVulnerabilityIndexAndPageAsync_ValidJson()
    {
        using var indexStream = new MemoryStream(Encoding.UTF8.GetBytes("""
        [
          {
            "@name": "base",
            "@id": "https://example.test/vulnerability.base.json",
            "@updated": "2024-01-02T03:04:05Z"
          }
        ]
        """));
        using var pageStream = new MemoryStream(Encoding.UTF8.GetBytes("""
        {
          "test.package": [
            {
              "url": "https://github.com/advisories/GHSA-xxxx-yyyy-zzzz",
              "severity": 2,
              "versions": "[1.0.0, 2.0.0)"
            }
          ]
        }
        """));

        IReadOnlyList<VulnerabilityIndexEntry>? index = await NuGetApi.GetVulnerabilityIndexAsync(indexStream);
        Dictionary<string, IList<PackageVulnerability>>? page = await NuGetApi.GetVulnerabilityPageAsync(pageStream);

        Assert.NotNull(index);
        Assert.Equal("base", index[0].Name);
        Assert.NotNull(page);
        Assert.Equal(2, page["test.package"][0].Severity);
    }
}
