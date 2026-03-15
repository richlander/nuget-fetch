using NuGetFetch;

HttpClient httpClient = new();
NuGetClient client = new(httpClient);
int passed = 0;

// Test 1: Get versions
Console.Write("Get versions... ");
IReadOnlyList<string> versions = await client.GetVersionsAsync("Newtonsoft.Json");
Console.WriteLine($"{versions.Count} versions ✓");
passed++;

// Test 2: Get latest version
Console.Write("Get latest version... ");
string? latest = await client.GetLatestVersionAsync("Newtonsoft.Json");
Console.WriteLine($"{latest} ✓");
passed++;

// Test 3: Parse package reference
Console.Write("Parse package reference... ");
PackageIdentity? id1 = PackageExtractor.ParsePackageReference("Foo@1.2.3");
PackageIdentity? id2 = PackageExtractor.ParsePackageReference("Bar");
Assert(id1?.Id == "Foo" && id1?.Version == "1.2.3");
Assert(id2?.Id == "Bar" && id2?.Version == "");
Console.WriteLine("✓");
passed++;

// Test 4: Source resolution
Console.Write("Source resolution... ");
IReadOnlyList<PackageSource> sources = SourceResolver.ResolveSources();
Assert(sources.Count > 0);
Console.WriteLine($"{sources.Count} source(s) ✓");
passed++;

// Test 5: TFM priorities
Console.Write("TFM priorities... ");
Assert(TfmResolver.GetTfmPriority("net10.0") > TfmResolver.GetTfmPriority("net8.0"));
Assert(TfmResolver.GetTfmPriority("net8.0") > TfmResolver.GetTfmPriority("netstandard2.0"));
Assert(TfmResolver.GetTfmPriority("netstandard2.0") > TfmResolver.GetTfmPriority("net461"));
Console.WriteLine("✓");
passed++;

// Test 6: Download, extract, resolve TFM
Console.Write("Download + extract + TFM resolve... ");
string tempDir = Path.Combine(Path.GetTempPath(), $"nugetfetch-{Guid.NewGuid():N}");
try
{
    using Stream nupkg = await client.DownloadAsync("Humanizer.Core", "2.14.1");
    await PackageExtractor.ExtractAsync(nupkg, tempDir);
    Assert(PackageExtractor.IsValidPackage(tempDir));

    string? tfmPath = TfmResolver.ResolvePackagePath(tempDir);
    Assert(tfmPath is not null);

    IReadOnlyList<PackageDll> dlls = TfmResolver.GetPackageDlls(tempDir);
    Assert(dlls.Count > 0);
    Console.WriteLine($"{dlls.Count} DLLs, best TFM: {Path.GetFileName(tfmPath)} ✓");
    passed++;
}
finally
{
    if (Directory.Exists(tempDir))
    {
        Directory.Delete(tempDir, true);
    }
}

// Test 7: Package cache
Console.Write("Package cache... ");
string cacheDir = Path.Combine(Path.GetTempPath(), $"nugetfetch-cache-{Guid.NewGuid():N}");
try
{
    // Use a temp directory as app name base
    PackageCache cache = new("nugetfetch-test");
    Assert(cache.TryGet("nonexistent-package", "1.0.0") is null);
    Console.WriteLine("✓");
    passed++;
}
finally
{
    if (Directory.Exists(cacheDir))
    {
        Directory.Delete(cacheDir, true);
    }
}

// Test 8: Version pattern resolution
Console.Write("Version pattern... ");
string? matched = await client.ResolveVersionPatternAsync("Newtonsoft.Json", "13.0.*");
Assert(matched is not null && matched.StartsWith("13.0."));
Console.WriteLine($"{matched} ✓");
passed++;

Console.WriteLine($"\n{passed}/{passed} tests passed ✅");

static void Assert(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? expr = null)
{
    if (!condition)
    {
        throw new Exception($"Assertion failed: {expr}");
    }
}
