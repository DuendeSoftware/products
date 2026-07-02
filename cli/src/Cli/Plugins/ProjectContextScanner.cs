// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using SimpleExec;

namespace Duende.Cli.Plugins;

/// <summary>
/// Resolves plugin versions by reading NuGet-resolved package versions from the project context.
/// Delegates project/solution discovery to <c>dotnet list package</c> (which uses the same
/// discovery logic as <c>dotnet build</c>) and reads the exact resolved version from the JSON output.
/// </summary>
internal static class ProjectContextScanner
{
    /// <summary>
    /// Resolves the plugin version from the current directory's project context.
    /// Runs <c>dotnet list package --format json</c> from <paramref name="searchDirectory"/>,
    /// letting dotnet discover the project or solution file, and extracts the resolved version
    /// of the anchor package.
    /// </summary>
    /// <param name="pluginName">The plugin name (e.g. "storage").</param>
    /// <param name="searchDirectory">The directory to run from. Defaults to current directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="PluginResolution"/> if the anchor package is found; otherwise <c>null</c>.</returns>
    internal static async Task<PluginResolution?> ResolveAsync(
        string pluginName,
        string? searchDirectory = null,
        CancellationToken ct = default)
    {
        if (!PluginRegistry.KnownPlugins.TryGetValue(pluginName, out var info))
        {
            return null;
        }

        var directory = searchDirectory ?? Directory.GetCurrentDirectory();

        var resolvedVersion = await GetResolvedVersionAsync(directory, info.AnchorPackage, ct);
        if (resolvedVersion is null)
        {
            return null;
        }

        return new PluginResolution(info.PackageId, $"[{resolvedVersion}]");
    }

    /// <summary>
    /// Creates a <see cref="PluginResolution"/> from an explicit version string (e.g. from <c>--plugin-version</c>).
    /// Uses exact version matching so that any version (including prereleases) resolves correctly.
    /// </summary>
    internal static PluginResolution? ResolveExplicit(string pluginName, string version)
    {
        if (!PluginRegistry.KnownPlugins.TryGetValue(pluginName, out var info))
        {
            return null;
        }

        return new PluginResolution(info.PackageId, $"[{version}]");
    }

    /// <summary>
    /// Runs <c>dotnet list package --format json</c> from the specified directory and extracts
    /// the resolved version of the anchor package. Delegates project/solution discovery entirely
    /// to <c>dotnet</c>, which looks for a <c>.csproj</c>, <c>.sln</c>, or <c>.slnx</c>
    /// in the working directory (same behavior as <c>dotnet build</c> without arguments).
    /// </summary>
    internal static async Task<string?> GetResolvedVersionAsync(
        string workingDirectory,
        string anchorPackage,
        CancellationToken ct)
    {
        try
        {
            var (stdout, _) = await Command.ReadAsync(
                "dotnet",
                "list package --format json",
                workingDirectory: workingDirectory,
                cancellationToken: ct);

            return ParseResolvedVersion(stdout, anchorPackage);
        }
        catch (ExitCodeReadException)
        {
            // dotnet list package failed (e.g. no project/solution found, restore not run)
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // dotnet not found or failed to start
            return null;
        }
    }

    /// <summary>
    /// Parses the JSON output of <c>dotnet list package --format json</c> to find the resolved version
    /// of the specified package. Searches across all projects in the output (handles solution-level listings).
    /// </summary>
    internal static string? ParseResolvedVersion(string json, string packageId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("projects", out var projects))
            {
                return null;
            }

            foreach (var project in projects.EnumerateArray())
            {
                if (!project.TryGetProperty("frameworks", out var frameworks))
                {
                    continue;
                }

                foreach (var framework in frameworks.EnumerateArray())
                {
                    if (!framework.TryGetProperty("topLevelPackages", out var packages))
                    {
                        continue;
                    }

                    foreach (var package in packages.EnumerateArray())
                    {
                        if (!package.TryGetProperty("id", out var id) ||
                            !package.TryGetProperty("resolvedVersion", out var version))
                        {
                            continue;
                        }

                        if (string.Equals(id.GetString(), packageId, StringComparison.OrdinalIgnoreCase))
                        {
                            return version.GetString();
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
