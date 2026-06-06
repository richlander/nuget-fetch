using NuGetFetch;
using Xunit;

namespace NuGetFetch.Tests;

/// <summary>
/// Integration tests for lean metadata endpoints that avoid NuGet.Protocol.
/// </summary>
[Collection("NuGet Integration")]
public class PackageMetadataServiceIntegrationTests : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly PackageMetadataService _metadata;

    public PackageMetadataServiceIntegrationTests()
    {
        _metadata = new PackageMetadataService(_http);
    }

    public void Dispose() => _http.Dispose();

    [Fact]
    public async Task GetRegistrationLeafAsync_ReturnsVersionSpecificMetadata()
    {
        RegistrationLeaf? leaf = await _metadata.GetRegistrationLeafAsync("System.Text.Json", "8.0.0");

        Assert.NotNull(leaf);
        Assert.True(leaf.Listed);
        Assert.NotNull(leaf.Published);
        Assert.Contains("/system.text.json/8.0.0/", leaf.PackageContent);
        Assert.NotNull(leaf.CatalogEntry);
    }

    [Fact]
    public async Task GetPackageMetadataAsync_ReturnsAggregatedMetadata()
    {
        PackageMetadata? metadata = await _metadata.GetPackageMetadataAsync(
            "System.Text.Json",
            "8.0.0",
            includeVulnerabilities: false);

        Assert.NotNull(metadata);
        Assert.Equal("System.Text.Json", metadata.Id);
        Assert.Equal("8.0.0", metadata.Version);
        Assert.NotNull(metadata.Published);
        Assert.True(metadata.TotalDownloads > 0);
        Assert.True(metadata.VersionCount > 0);
        Assert.True(metadata.PackageSize > 0);
    }

    [Fact]
    public async Task GetVulnerabilitiesAsync_ReturnsAffectedRanges()
    {
        IReadOnlyList<PackageVulnerability> vulnerabilities =
            await _metadata.GetVulnerabilitiesAsync("System.Text.Json", "8.0.0");

        Assert.Contains(vulnerabilities, v => v.Url.Contains("github.com/advisories/", StringComparison.OrdinalIgnoreCase));
    }
}
