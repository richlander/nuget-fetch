#:package NuGet.Versioning@7.0.1
#:project NuGetFetch/NuGetFetch.csproj

using System.Runtime.InteropServices;
using System.Xml.Linq;
using NuGetFetch;

string packageId = args.Length > 0 ? args[0] : "dotnet-inspect";
string? requestedVersion = args.Length > 1 ? args[1] : null;
string rid = RuntimeInformation.RuntimeIdentifier;

Console.WriteLine($"RID: {rid}");
Console.WriteLine();

using HttpClient http = new();
NuGetClient client = new(http);

// Resolve version
string? version = requestedVersion ?? await client.GetLatestVersionAsync(packageId);
if (version is null)
{
    Console.Error.WriteLine($"Package '{packageId}' not found.");
    return 1;
}

Console.WriteLine($"Package: {packageId} v{version}");

// Download and extract the main package
string extractPath = await DownloadPackageAsync(client, packageId, version);

// Look for DotnetToolSettings.xml with RID-specific packages
string? toolExecutable = ResolveToolExecutable(extractPath, rid);
if (toolExecutable is not null)
{
    Console.WriteLine($"Tool:    {toolExecutable}");
    return 0;
}

// Check for RuntimeIdentifierPackages in tool settings
string? ridPackageId = ResolveRidPackageId(extractPath, rid);
if (ridPackageId is not null)
{
    Console.WriteLine($"RID package: {ridPackageId}");
    string ridExtractPath = await DownloadPackageAsync(client, ridPackageId, version);

    toolExecutable = ResolveToolExecutable(ridExtractPath, rid);
    if (toolExecutable is not null)
    {
        Console.WriteLine($"Tool:        {toolExecutable}");
        return 0;
    }
}

// Fall back to best TFM
string? tfmPath = TfmResolver.ResolvePackagePath(extractPath);
if (tfmPath is not null)
{
    Console.WriteLine($"Best TFM: {Path.GetRelativePath(extractPath, tfmPath)}");
}

return 0;

async Task<string> DownloadPackageAsync(NuGetClient client, string id, string version)
{
    string path = Path.Combine(Path.GetTempPath(), "nuget-fetch-demo", id, version);
    if (Directory.Exists(path))
    {
        Console.WriteLine($"  Cached:      {path}");
        return path;
    }

    Console.Write($"  Downloading: {id}... ");
    using Stream nupkg = await client.DownloadAsync(id, version);
    await PackageExtractor.ExtractAsync(nupkg, path);
    Console.WriteLine("done.");
    return path;
}

// Find the native executable for a RID inside a tools/ layout
static string? ResolveToolExecutable(string extractPath, string rid)
{
    // NAOT tools: tools/any/{rid}/{exe} or tools/{tfm}/{rid}/{exe}
    string toolsDir = Path.Combine(extractPath, "tools");
    if (!Directory.Exists(toolsDir))
        return null;

    foreach (string tfmDir in Directory.GetDirectories(toolsDir))
    {
        string ridDir = Path.Combine(tfmDir, rid);
        if (!Directory.Exists(ridDir))
            continue;

        string? settingsFile = Directory.GetFiles(ridDir, "DotnetToolSettings.xml").FirstOrDefault();
        if (settingsFile is null)
            continue;

        XDocument settings = XDocument.Load(settingsFile);
        string? entryPoint = settings.Descendants("Command")
            .FirstOrDefault()?.Attribute("EntryPoint")?.Value;

        if (entryPoint is null)
            continue;

        string executable = Path.Combine(ridDir, entryPoint);
        if (File.Exists(executable))
            return executable;
    }

    return null;
}

// Parse the main package's DotnetToolSettings.xml for RuntimeIdentifierPackages
static string? ResolveRidPackageId(string extractPath, string rid)
{
    string toolsDir = Path.Combine(extractPath, "tools");
    if (!Directory.Exists(toolsDir))
        return null;

    foreach (string settingsFile in Directory.EnumerateFiles(toolsDir, "DotnetToolSettings.xml", SearchOption.AllDirectories))
    {
        XDocument settings = XDocument.Load(settingsFile);
        var ridPackages = settings.Descendants("RuntimeIdentifierPackage");

        // Exact RID match
        string? packageId = ridPackages
            .FirstOrDefault(e => string.Equals(
                e.Attribute("RuntimeIdentifier")?.Value, rid, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Id")?.Value;

        if (packageId is not null)
            return packageId;

        // Fallback to "any"
        packageId = ridPackages
            .FirstOrDefault(e => string.Equals(
                e.Attribute("RuntimeIdentifier")?.Value, "any", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Id")?.Value;

        if (packageId is not null)
            return packageId;
    }

    return null;
}
