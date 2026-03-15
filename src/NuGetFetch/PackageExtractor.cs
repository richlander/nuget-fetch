using System.IO.Compression;

namespace NuGetFetch;

/// <summary>
/// Extracts .nupkg (ZIP) archives and parses package references.
/// </summary>
public static class PackageExtractor
{
    /// <summary>
    /// Extracts a .nupkg stream to a destination directory.
    /// </summary>
    public static async Task<string> ExtractAsync(Stream nupkg, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        // Copy to a temp file first (ZipFile needs seekable stream)
        string tempFile = Path.GetTempFileName();

        try
        {
            using (FileStream temp = File.Create(tempFile))
            {
                await nupkg.CopyToAsync(temp);
            }

            ZipFile.ExtractToDirectory(tempFile, destinationPath, overwriteFiles: true);
        }
        finally
        {
            File.Delete(tempFile);
        }

        return destinationPath;
    }

    /// <summary>
    /// Extracts a local .nupkg file to a destination directory.
    /// </summary>
    public static string Extract(string nupkgPath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);
        ZipFile.ExtractToDirectory(nupkgPath, destinationPath, overwriteFiles: true);
        return destinationPath;
    }

    /// <summary>
    /// Parses a package reference string like "Newtonsoft.Json@13.0.3" or "Newtonsoft.Json".
    /// </summary>
    public static PackageIdentity? ParsePackageReference(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return null;
        }

        int atIndex = spec.IndexOf('@');

        if (atIndex < 0)
        {
            return new PackageIdentity(spec.Trim(), string.Empty);
        }

        string id = spec[..atIndex].Trim();
        string version = spec[(atIndex + 1)..].Trim();

        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return new PackageIdentity(id, version);
    }

    /// <summary>
    /// Checks whether an extracted package directory looks valid
    /// (contains .nuspec and/or lib or tools directories).
    /// </summary>
    public static bool IsValidPackage(string extractedPath)
    {
        if (!Directory.Exists(extractedPath))
        {
            return false;
        }

        bool hasNuspec = Directory.GetFiles(extractedPath, "*.nuspec").Length > 0;
        bool hasLib = Directory.Exists(Path.Combine(extractedPath, "lib"));
        bool hasTools = Directory.Exists(Path.Combine(extractedPath, "tools"));

        return hasNuspec || hasLib || hasTools;
    }
}
