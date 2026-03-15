using System.Xml.Linq;

namespace NuGetFetch;

/// <summary>
/// Resolves NuGet package sources from nuget.config files.
/// </summary>
public static class SourceResolver
{
    private static readonly string NuGetOrgName = "nuget.org";
    private static readonly string NuGetOrgUrl = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// Resolves NuGet sources in priority order.
    /// </summary>
    public static IReadOnlyList<PackageSource> ResolveSources(
        string? explicitSource = null,
        string? configPath = null,
        IEnumerable<string>? additionalSources = null)
    {
        // Explicit source overrides everything
        if (explicitSource is not null)
        {
            return [new PackageSource("explicit", explicitSource)];
        }

        List<PackageSource> sources = [];

        // Load from config file(s)
        if (configPath is not null)
        {
            sources.AddRange(LoadSourcesFromConfig(configPath));
        }
        else
        {
            foreach (string file in FindConfigFiles())
            {
                sources.AddRange(LoadSourcesFromConfig(file));
            }
        }

        // Default to nuget.org if no config sources found
        if (sources.Count == 0)
        {
            sources.Add(new PackageSource(NuGetOrgName, NuGetOrgUrl));
        }

        // Append additional sources
        if (additionalSources is not null)
        {
            foreach (string url in additionalSources)
            {
                sources.Add(new PackageSource("additional", url));
            }
        }

        return sources;
    }

    /// <summary>
    /// Finds nuget.config files by walking up the directory tree from the current directory.
    /// </summary>
    public static IReadOnlyList<string> FindConfigFiles(string? startDir = null)
    {
        List<string> configs = [];
        string? dir = startDir ?? Directory.GetCurrentDirectory();

        while (dir is not null)
        {
            string configFile = Path.Combine(dir, "nuget.config");

            if (File.Exists(configFile))
            {
                configs.Add(configFile);
            }

            // Also check NuGet.Config (case variation)
            string configFile2 = Path.Combine(dir, "NuGet.Config");

            if (File.Exists(configFile2) && !configs.Contains(configFile2))
            {
                configs.Add(configFile2);
            }

            dir = Path.GetDirectoryName(dir);
        }

        // User-level config
        string? userConfig = GetUserConfigPath();

        if (userConfig is not null && File.Exists(userConfig))
        {
            configs.Add(userConfig);
        }

        return configs;
    }

    /// <summary>
    /// Loads package sources from a nuget.config file.
    /// </summary>
    public static IReadOnlyList<PackageSource> LoadSourcesFromConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return [];
        }

        try
        {
            XDocument doc = XDocument.Load(configPath);
            XElement? root = doc.Root;

            if (root is null)
            {
                return [];
            }

            // Parse <packageSources>
            Dictionary<string, string> sources = [];
            XElement? packageSources = root.Element("packageSources");

            if (packageSources is not null)
            {
                foreach (XElement element in packageSources.Elements())
                {
                    if (element.Name == "clear")
                    {
                        sources.Clear();
                        continue;
                    }

                    if (element.Name == "add")
                    {
                        string? key = element.Attribute("key")?.Value;
                        string? value = element.Attribute("value")?.Value;

                        if (key is not null && value is not null)
                        {
                            sources[key] = value;
                        }
                    }
                }
            }

            // Parse <disabledPackageSources>
            HashSet<string> disabled = [];
            XElement? disabledSources = root.Element("disabledPackageSources");

            if (disabledSources is not null)
            {
                foreach (XElement element in disabledSources.Elements("add"))
                {
                    string? key = element.Attribute("key")?.Value;
                    string? value = element.Attribute("value")?.Value;

                    if (key is not null && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        disabled.Add(key);
                    }
                }
            }

            // Parse <packageSourceCredentials>
            Dictionary<string, PackageSourceCredential> credentials = [];
            XElement? credentialsElement = root.Element("packageSourceCredentials");

            if (credentialsElement is not null)
            {
                foreach (XElement sourceElement in credentialsElement.Elements())
                {
                    // Source name may be XML-encoded (spaces → _x0020_)
                    string sourceName = sourceElement.Name.LocalName.Replace("_x0020_", " ");
                    string? username = null;
                    string? password = null;

                    foreach (XElement add in sourceElement.Elements("add"))
                    {
                        string? key = add.Attribute("key")?.Value;
                        string? value = add.Attribute("value")?.Value;

                        if (string.Equals(key, "Username", StringComparison.OrdinalIgnoreCase))
                        {
                            username = value;
                        }
                        else if (string.Equals(key, "ClearTextPassword", StringComparison.OrdinalIgnoreCase))
                        {
                            password = value;
                        }
                    }

                    if (username is not null && password is not null)
                    {
                        credentials[sourceName] = new PackageSourceCredential(username, password);
                    }
                }
            }

            // Build result
            List<PackageSource> result = [];

            foreach ((string name, string url) in sources)
            {
                if (disabled.Contains(name))
                {
                    continue;
                }

                credentials.TryGetValue(name, out PackageSourceCredential? credential);
                result.Add(new PackageSource(name, url, credential));
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    private static string? GetUserConfigPath()
    {
        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "NuGet", "NuGet.Config");
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".nuget", "NuGet", "NuGet.Config");
    }
}
