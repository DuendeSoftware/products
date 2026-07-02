// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
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
    internal static async Task<string> EnsureDownloadedAsync(PluginResolution resolution, Ct ct)
    {
        // Check if any version matching the range is already cached
        var cachedPath = FindCachedAssembly(resolution.PackageId, resolution.Version);
        if (cachedPath is not null)
        {
            return cachedPath;
        }

        // Download from nuget.org
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(ct);

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

        using var packageStream = new MemoryStream();
        var downloaded = await resource.CopyNupkgToStreamAsync(
            resolution.PackageId, bestMatch, packageStream, cache, NullLogger.Instance, ct);

        if (!downloaded)
        {
            throw new InvalidOperationException(
                $"Failed to download '{resolution.PackageId}' {bestMatch} from nuget.org.");
        }

        packageStream.Position = 0;

        // Extract to global packages folder — NuGet convention requires lowercase package IDs in path
#pragma warning disable CA1308 // NuGet global packages folder uses lowercase package IDs by convention
        var packageDir = Path.Combine(GlobalPackagesFolder, resolution.PackageId.ToLowerInvariant(), bestMatch.ToString());
#pragma warning restore CA1308

        // Use a temp directory + atomic rename to avoid races between concurrent invocations
        var tempDir = $"{packageDir}.{Path.GetRandomFileName()}";
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            using var packageReader = new PackageArchiveReader(packageStream);
            var tfmFolder = GetBestTfmFolder(packageReader);

            // Strip the NuGet TFM prefix (e.g. "lib/net10.0/") but preserve the remaining
            // subdirectory structure. AssemblyDependencyResolver reads runtimeTargets from the
            // deps.json and expects RID-specific assemblies at relative paths from the plugin
            // assembly (e.g. "runtimes/win/lib/net9.0/Microsoft.Data.SqlClient.dll").
            var prefix = tfmFolder.TrimEnd('/') + "/";
            var tempDirRoot = Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar;
            foreach (var file in packageReader.GetFiles(tfmFolder))
            {
                var relativePath = file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? file[prefix.Length..]
                    : Path.GetFileName(file);

                var destPath = Path.GetFullPath(Path.Combine(tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));

                // Guard against path traversal from malicious package entries.
                // The trailing separator prevents prefix false-positives
                // (e.g. "C:\temp\pkg2\..." falsely matching "C:\temp\pkg").
                if (!destPath.StartsWith(tempDirRoot, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Package contains a file entry that would extract outside the target directory: '{file}'.");
                }

                var destDir = Path.GetDirectoryName(destPath);
                if (destDir is not null)
                {
                    _ = Directory.CreateDirectory(destDir);
                }

                using var fileStream = packageReader.GetStream(file);
                using var dest = File.Create(destPath);
                await fileStream.CopyToAsync(dest, ct);
            }

            try
            {
                Directory.Move(tempDir, packageDir);
            }
            catch (IOException) when (Directory.Exists(packageDir))
            {
                // Another process won the race — use their copy
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch
        {
            // Clean up temp directory on any failure
#pragma warning disable CA1031 // Best-effort cleanup — must not mask the original exception
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
#pragma warning restore CA1031
            throw;
        }

        return FindAssemblyInDirectory(packageDir, resolution.PackageId)
            ?? throw new InvalidOperationException(
                $"Could not find plugin assembly in downloaded package '{resolution.PackageId}'.");
    }

    private static string? FindCachedAssembly(string packageId, string versionRange)
    {
#pragma warning disable CA1308 // NuGet global packages folder uses lowercase package IDs by convention
        var packageDir = Path.Combine(GlobalPackagesFolder, packageId.ToLowerInvariant());
#pragma warning restore CA1308
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
            .OfType<NuGetVersion>();

        var best = range.FindBestMatch(installedVersions);
        if (best is null)
        {
            return null;
        }

        var versionDir = Path.Combine(packageDir, best.ToString());
        return FindAssemblyInDirectory(versionDir, packageId);
    }

    private static string? FindAssemblyInDirectory(string directory, string packageId)
    {
        var assemblyName = $"{packageId}.dll";
        var path = Path.Combine(directory, assemblyName);
        return File.Exists(path) ? path : null;
    }

    private static string GetBestTfmFolder(PackageArchiveReader reader)
    {
        // Pick the highest TFM version available in the package
        var libItems = reader.GetLibItems().ToList();
        var preferred = libItems
            .OrderByDescending(g => g.TargetFramework.Version)
            .FirstOrDefault();

        return preferred?.TargetFramework.GetShortFolderName() is { } tfm
            ? $"lib/{tfm}"
            : "lib";
    }
}
