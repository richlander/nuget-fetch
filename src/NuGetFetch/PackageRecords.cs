namespace NuGetFetch;

public record PackageIdentity(string Id, string Version);

public record PackageSource(string Name, string Url, PackageSourceCredential? Credential = null)
{
    public bool IsNuGetOrg => Url.Contains("api.nuget.org", StringComparison.OrdinalIgnoreCase);

    public string? GetFlatContainerUrl() =>
        IsNuGetOrg ? NuGetClient.NuGetOrgFlatContainer : null;
}

public record PackageSourceCredential(string Username, string Password);

public record ExtractionResult(
    string Path,
    string Id,
    string Version,
    bool FromCache);

public record PackageDll(string Path, string? Tfm);
