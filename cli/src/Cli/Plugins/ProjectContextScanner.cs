// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using System.Xml.Linq;

namespace Duende.Cli.Plugins;

/// <summary>
/// Scans the current directory and its ancestors for NuGet package references
/// to determine the version of a plugin's anchor package.
/// Supports Central Package Management (<c>Directory.Packages.props</c>),
/// direct <c>.csproj</c> references, and deployed assembly scanning.
/// </summary>
internal static class ProjectContextScanner
{
    private const string DirectoryPackagesProps = "Directory.Packages.props";

    /// <summary>
    /// Resolves the plugin version using the full fallback chain:
    /// project context → deployed assembly scanning.
    /// </summary>
    /// <param name="pluginName">The plugin name (e.g. "storage").</param>
    /// <param name="searchDirectory">The directory to start scanning from. Defaults to current directory.</param>
    /// <returns>A <see cref="PluginResolution"/> if the anchor package is found; otherwise <c>null</c>.</returns>
    internal static PluginResolution? Resolve(string pluginName, string? searchDirectory = null)
    {
        if (!PluginRegistry.KnownPlugins.TryGetValue(pluginName, out var info))
        {
            return null;
        }

        var directory = searchDirectory ?? Directory.GetCurrentDirectory();

        // Strategy 1: scan project files (Directory.Packages.props, .csproj)
        var anchorVersion = FindAnchorVersionFromProjectFiles(info.AnchorPackage, directory);

        // Strategy 2: scan for deployed anchor assembly
        anchorVersion ??= FindAnchorVersionFromDeployedAssembly(info.AnchorPackage, directory);

        if (anchorVersion is null)
        {
            return null;
        }

        var pluginVersion = ToMajorMinorWildcard(anchorVersion);
        return new PluginResolution(info.PackageId, pluginVersion);
    }

    /// <summary>
    /// Creates a <see cref="PluginResolution"/> from an explicit version string (e.g. from <c>--plugin-version</c>).
    /// </summary>
    internal static PluginResolution? ResolveExplicit(string pluginName, string version)
    {
        if (!PluginRegistry.KnownPlugins.TryGetValue(pluginName, out var info))
        {
            return null;
        }

        var pluginVersion = ToMajorMinorWildcard(version);
        return new PluginResolution(info.PackageId, pluginVersion);
    }

    private static string? FindAnchorVersionFromProjectFiles(string anchorPackage, string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            // Check Directory.Packages.props (Central Package Management)
            var packagesProps = Path.Combine(directory.FullName, DirectoryPackagesProps);
            if (File.Exists(packagesProps))
            {
                var version = ExtractVersionFromPackagesProps(packagesProps, anchorPackage);
                if (version is not null)
                {
                    return version;
                }
            }

            // Check .csproj files in this directory
            try
            {
                foreach (var csproj in directory.GetFiles("*.csproj"))
                {
                    var version = ExtractVersionFromCsproj(csproj.FullName, anchorPackage);
                    if (version is not null)
                    {
                        return version;
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
            {
                // Skip inaccessible directories
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Scans the directory for a deployed anchor assembly (e.g. <c>Duende.Storage.dll</c>)
    /// and reads its version information to determine the product version.
    /// Uses <see cref="System.Diagnostics.FileVersionInfo"/> to avoid loading the assembly.
    /// </summary>
    private static string? FindAnchorVersionFromDeployedAssembly(string anchorPackage, string directory)
    {
        var assemblyFileName = $"{anchorPackage}.dll";
        var assemblyPath = Path.Combine(directory, assemblyFileName);

        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        try
        {
            // FileVersionInfo reads the PE version resource without loading the assembly
            var fileInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyPath);

            // ProductVersion corresponds to AssemblyInformationalVersionAttribute
            if (fileInfo.ProductVersion is { Length: > 0 } productVersion)
            {
                // Strip build metadata (e.g. "7.2.1+abc123" → "7.2.1")
                var plusIndex = productVersion.IndexOf('+', StringComparison.Ordinal);
                return plusIndex >= 0 ? productVersion[..plusIndex] : productVersion;
            }

            // Fall back to FileVersion (corresponds to AssemblyFileVersionAttribute)
            if (fileInfo.FileMajorPart > 0)
            {
                return $"{fileInfo.FileMajorPart}.{fileInfo.FileMinorPart}.{fileInfo.FileBuildPart}";
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ExtractVersionFromPackagesProps(string filePath, string packageId)
    {
        try
        {
            var doc = XDocument.Load(filePath);
            return doc.Descendants("PackageVersion")
                .FirstOrDefault(e =>
                    string.Equals(e.Attribute("Include")?.Value, packageId, StringComparison.OrdinalIgnoreCase))
                ?.Attribute("Version")?.Value;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ExtractVersionFromCsproj(string filePath, string packageId)
    {
        try
        {
            var doc = XDocument.Load(filePath);
            return doc.Descendants("PackageReference")
                .FirstOrDefault(e =>
                    string.Equals(e.Attribute("Include")?.Value, packageId, StringComparison.OrdinalIgnoreCase))
                ?.Attribute("Version")?.Value;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a version string like "7.2.1" to "7.2.*" for NuGet floating version resolution.
    /// </summary>
    internal static string ToMajorMinorWildcard(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}.*" : $"{version}.*";
    }
}
