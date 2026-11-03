// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Duende.Cli.Plugins;

/// <summary>
/// Resolves and downloads CLI plugin packages from nuget.org using the NuGet global packages folder as cache.
/// </summary>
internal static class NuGetPluginResolver
{
    private static readonly string GlobalPackagesFolder =
        SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(null));

    /// <summary>
    /// Ensures the plugin package is available in the NuGet global packages folder,
    /// downloading it from nuget.org if not already cached.
    /// </summary>
    /// <param name="resolution">The resolved plugin package ID and version range (e.g. "7.2.*").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The path to the plugin's primary assembly.</returns>
    internal static Task<string> EnsureDownloadedAsync(PluginResolution resolution, Ct ct) =>
        EnsureDownloadedAsync(
            resolution,
            GlobalPackagesFolder,
            Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json"),
            ct);

    internal static async Task<string> EnsureDownloadedAsync(
        PluginResolution resolution,
        string globalPackagesFolder,
        SourceRepository source,
        Ct ct)
    {
        // Check if any version matching the range is already cached
        var cachedPath = FindCachedAssembly(globalPackagesFolder, resolution.PackageId, resolution.Version);
        if (cachedPath is not null)
        {
            return cachedPath;
        }

        var resource = await source.GetResourceAsync<FindPackageByIdResource>(ct);

        using var cache = new SourceCacheContext();
        var versions = await resource.GetAllVersionsAsync(resolution.PackageId, cache, NullLogger.Instance, ct);

        if (!VersionRange.TryParse(resolution.Version, out var versionRange))
        {
            throw new InvalidOperationException(
                $"'{resolution.Version}' is not a valid NuGet version range for '{resolution.PackageId}'.");
        }

        var bestMatch = versionRange.FindBestMatch(versions)
            ?? throw new InvalidOperationException(
                $"No version of '{resolution.PackageId}' matching '{resolution.Version}' was found on nuget.org.");

        Console.WriteLine($"Downloading {resolution.PackageId} {bestMatch}...");

        var identity = new PackageIdentity(resolution.PackageId, bestMatch);
        using var downloader = await resource.GetPackageDownloaderAsync(identity, cache, NullLogger.Instance, ct)
            ?? throw new InvalidOperationException(
                $"Failed to download '{resolution.PackageId}' {bestMatch} from nuget.org.");

        var pathResolver = new VersionFolderPathResolver(globalPackagesFolder);
        _ = await PackageExtractor.InstallFromSourceAsync(
            identity,
            downloader,
            pathResolver,
            new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None, clientPolicyContext: null, NullLogger.Instance),
            ct);

        return FindAssemblyInDirectory(pathResolver.GetInstallPath(resolution.PackageId, bestMatch), resolution.PackageId)
            ?? throw new InvalidOperationException(
                $"Could not find plugin assembly in downloaded package '{resolution.PackageId}'.");
    }

    private static string? FindCachedAssembly(string globalPackagesFolder, string packageId, string versionRange)
    {
        var pathResolver = new VersionFolderPathResolver(globalPackagesFolder);
        var packageDir = pathResolver.GetVersionListPath(packageId);
        if (!Directory.Exists(packageDir))
        {
            return null;
        }

        if (!VersionRange.TryParse(versionRange, out var range))
        {
            return null;
        }

        var installedVersions = Directory.GetDirectories(packageDir)
            .Select(d => NuGetVersion.TryParse(Path.GetFileName(d), out var v) ? v : null)
            .OfType<NuGetVersion>()
            .Where(v => File.Exists(pathResolver.GetNupkgMetadataPath(packageId, v)));

        var best = range.FindBestMatch(installedVersions);
        if (best is null)
        {
            return null;
        }

        return FindAssemblyInDirectory(pathResolver.GetInstallPath(packageId, best), packageId);
    }

    private static string? FindAssemblyInDirectory(string packageDirectory, string packageId)
    {
        var libDirectory = Path.Combine(packageDirectory, "lib");
        if (!Directory.Exists(libDirectory))
        {
            return null;
        }

        var tfmDirectory = Directory.GetDirectories(libDirectory)
            .OrderByDescending(d => NuGetFramework.ParseFolder(Path.GetFileName(d)).Version)
            .FirstOrDefault();
        if (tfmDirectory is null)
        {
            return null;
        }

        var path = Path.Combine(tfmDirectory, $"{packageId}.dll");
        return File.Exists(path) ? path : null;
    }
}
