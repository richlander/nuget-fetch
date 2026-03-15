namespace NuGetFetch;

/// <summary>
/// Resolves Target Framework Monikers (TFMs) in extracted NuGet packages.
/// Selects the highest-priority .NET TFM available.
/// </summary>
public static class TfmResolver
{
    /// <summary>
    /// Resolves the best assembly path for a specific or auto-selected TFM.
    /// Returns the path to the TFM directory, or null if not found.
    /// </summary>
    public static string? ResolvePackagePath(string extractedPath, string? tfm = null)
    {
        if (tfm is not null)
        {
            return FindByTfm(extractedPath, tfm);
        }

        return FindHighestTfm(extractedPath);
    }

    /// <summary>
    /// Gets all DLLs in a package, grouped by TFM.
    /// </summary>
    public static IReadOnlyList<PackageDll> GetPackageDlls(string extractedPath)
    {
        List<PackageDll> dlls = [];

        // Check lib/ directory
        string libDir = Path.Combine(extractedPath, "lib");

        if (Directory.Exists(libDir))
        {
            CollectDlls(libDir, dlls);
        }

        // Check tools/ directory
        string toolsDir = Path.Combine(extractedPath, "tools");

        if (Directory.Exists(toolsDir))
        {
            CollectDlls(toolsDir, dlls);
        }

        return dlls;
    }

    /// <summary>
    /// Gets the priority score for a TFM. Higher is better.
    /// </summary>
    public static int GetTfmPriority(string tfm)
    {
        ReadOnlySpan<char> span = tfm.AsSpan();

        // Modern .NET (net5.0+)
        if (span.StartsWith("net") && !span.StartsWith("netstandard") && !span.StartsWith("netcoreapp"))
        {
            ReadOnlySpan<char> versionPart = span[3..];
            int dotIndex = versionPart.IndexOf('.');

            if (dotIndex > 0 &&
                int.TryParse(versionPart[..dotIndex], out int major) &&
                major >= 5)
            {
                int minor = 0;

                if (dotIndex + 1 < versionPart.Length)
                {
                    int.TryParse(versionPart[(dotIndex + 1)..], out minor);
                }

                return 10000 + (major * 100) + minor;
            }

            // Legacy .NET Framework (net45, net461, etc.)
            if (int.TryParse(versionPart, out int frameworkVersion))
            {
                return 1000 + frameworkVersion;
            }
        }

        // .NET Core (netcoreapp2.1, netcoreapp3.1)
        if (span.StartsWith("netcoreapp"))
        {
            ReadOnlySpan<char> versionPart = span[10..];
            int dotIndex = versionPart.IndexOf('.');

            if (dotIndex > 0 &&
                int.TryParse(versionPart[..dotIndex], out int major))
            {
                int minor = 0;

                if (dotIndex + 1 < versionPart.Length)
                {
                    int.TryParse(versionPart[(dotIndex + 1)..], out minor);
                }

                return 5000 + (major * 100) + minor;
            }
        }

        // .NET Standard
        if (span.StartsWith("netstandard"))
        {
            ReadOnlySpan<char> versionPart = span[11..];
            int dotIndex = versionPart.IndexOf('.');

            if (dotIndex > 0 &&
                int.TryParse(versionPart[..dotIndex], out int major))
            {
                int minor = 0;

                if (dotIndex + 1 < versionPart.Length)
                {
                    int.TryParse(versionPart[(dotIndex + 1)..], out minor);
                }

                return 3000 + (major * 100) + minor;
            }
        }

        return 0;
    }

    private static string? FindByTfm(string extractedPath, string tfm)
    {
        // Check lib/{tfm} and tools/{tfm}
        foreach (string subdir in new[] { "lib", "tools" })
        {
            string dir = Path.Combine(extractedPath, subdir, tfm);

            if (Directory.Exists(dir))
            {
                return dir;
            }

            // Case-insensitive fallback
            string parent = Path.Combine(extractedPath, subdir);

            if (Directory.Exists(parent))
            {
                foreach (string candidate in Directory.GetDirectories(parent))
                {
                    if (string.Equals(Path.GetFileName(candidate), tfm, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private static string? FindHighestTfm(string extractedPath)
    {
        string? bestPath = null;
        int bestPriority = -1;

        foreach (string subdir in new[] { "lib", "tools" })
        {
            string parent = Path.Combine(extractedPath, subdir);

            if (!Directory.Exists(parent))
            {
                continue;
            }

            foreach (string tfmDir in Directory.GetDirectories(parent))
            {
                string tfmName = Path.GetFileName(tfmDir);
                int priority = GetTfmPriority(tfmName);

                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestPath = tfmDir;
                }
            }
        }

        return bestPath;
    }

    private static void CollectDlls(string baseDir, List<PackageDll> dlls)
    {
        foreach (string tfmDir in Directory.GetDirectories(baseDir))
        {
            string tfmName = Path.GetFileName(tfmDir);

            if (!IsTfmLike(tfmName))
            {
                continue;
            }

            foreach (string dll in Directory.GetFiles(tfmDir, "*.dll"))
            {
                dlls.Add(new PackageDll(dll, tfmName));
            }
        }
    }

    private static bool IsTfmLike(string name) =>
        name.StartsWith("net", StringComparison.OrdinalIgnoreCase);
}
