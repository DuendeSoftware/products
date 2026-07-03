// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Text.Json;

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
    /// Runs <c>dotnet list package --format json --include-transitive</c> from <paramref name="searchDirectory"/>,
    /// letting dotnet discover the project or solution file, and extracts the resolved version
    /// of the anchor package from either top-level or transitive packages.
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
    /// Runs <c>dotnet list package --format json --include-transitive</c> from the specified directory
    /// and extracts the resolved version of the anchor package. Delegates project/solution discovery
    /// entirely to <c>dotnet</c>, which looks for a <c>.csproj</c>, <c>.sln</c>, or <c>.slnx</c>
    /// in the working directory (same behavior as <c>dotnet build</c> without arguments).
    /// </summary>
    internal static async Task<string?> GetResolvedVersionAsync(
        string workingDirectory,
        string anchorPackage,
        CancellationToken ct)
    {
        try
        {
            var stdout = await RunDotnetListPackageAsync(workingDirectory, ct);
            return ParseResolvedVersion(stdout, anchorPackage);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // dotnet not found or failed to start
            return null;
        }
    }

    /// <summary>
    /// Executes <c>dotnet list package --format json --include-transitive</c> and returns stdout.
    /// </summary>
    private static async Task<string> RunDotnetListPackageAsync(
        string workingDirectory,
        CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "list package --format json --include-transitive",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!process.Start())
        {
            return string.Empty;
        }

        // Drain both streams concurrently to prevent deadlocks when pipe buffers fill.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        var stdout = await stdoutTask;
        _ = await stderrTask;

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            return string.Empty;
        }

        return stdout;
    }

    /// <summary>
    /// Parses the JSON output of <c>dotnet list package --format json --include-transitive</c> to find
    /// the resolved version of the specified package. Searches both <c>topLevelPackages</c> and
    /// <c>transitivePackages</c> across all projects in the output (handles solution-level listings).
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
                    var version = FindPackageInArray(framework, "topLevelPackages", packageId)
                                  ?? FindPackageInArray(framework, "transitivePackages", packageId);

                    if (version is not null)
                    {
                        return version;
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

    private static string? FindPackageInArray(JsonElement framework, string arrayName, string packageId)
    {
        if (!framework.TryGetProperty(arrayName, out var packages))
        {
            return null;
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

        return null;
    }
}
