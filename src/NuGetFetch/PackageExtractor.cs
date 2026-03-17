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
    public static async Task<string> ExtractAsync(Stream nupkg, string destinationPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationPath);

        // Copy to a temp file first (ZipFile needs seekable stream)
        string tempFile = Path.GetTempFileName();

        try
        {
            using (FileStream temp = File.Create(tempFile))
            {
                await nupkg.CopyToAsync(temp, cancellationToken).ConfigureAwait(false);
            }

            ExtractWithValidation(tempFile, destinationPath);
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
        ExtractWithValidation(nupkgPath, destinationPath);
        return destinationPath;
    }

    /// <summary>
    /// Extracts a zip file with defense-in-depth path traversal validation.
    /// Rejects entries with absolute paths, '..' segments, or paths
    /// that would escape the destination directory.
    /// </summary>
    private static void ExtractWithValidation(string zipPath, string destinationPath)
    {
        string fullDestination = Path.GetFullPath(destinationPath);

        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName))
            {
                continue;
            }

            // Reject entries with path traversal components
            if (entry.FullName.Contains("..", StringComparison.Ordinal)
                || Path.IsPathRooted(entry.FullName)
                || entry.FullName.Contains('\0'))
            {
                throw new InvalidDataException($"Zip entry '{entry.FullName}' contains invalid path components.");
            }

            string targetPath = Path.GetFullPath(Path.Combine(fullDestination, entry.FullName));

            if (!targetPath.StartsWith(fullDestination + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !targetPath.Equals(fullDestination, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Zip entry '{entry.FullName}' would extract outside the target directory.");
            }

            // Directory entry
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            // File entry
            string? parentDir = Path.GetDirectoryName(targetPath);

            if (parentDir is not null)
            {
                Directory.CreateDirectory(parentDir);
            }

            entry.ExtractToFile(targetPath, overwrite: true);
        }
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
            return new PackageIdentity(spec.Trim(), null);
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
