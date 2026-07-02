// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Cli.Plugins;

/// <summary>
/// Detects which plugin the user is invoking from raw args and eagerly loads it
/// before the command tree is built. This ensures <c>duende storage --help</c>
/// shows real subcommands from the plugin rather than the stub help.
/// </summary>
internal static class PluginPreloader
{
    private const string PluginPathFlag = "--plugin-path";
    private const string VersionFlag = "--plugin-version";

    /// <summary>
    /// Scans <paramref name="args"/> for a known plugin name as the first
    /// non-option argument (e.g. "storage" in "duende storage --help").
    /// Returns the plugin name or <c>null</c> if none detected.
    /// </summary>
    internal static string? DetectRequestedPlugin(string[] args)
    {
        var skipNext = false;
        foreach (var arg in args)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            // Skip option flags that take a value
            if (arg is PluginPathFlag or VersionFlag)
            {
                skipNext = true;
                continue;
            }

            if (arg.StartsWith('-'))
            {
                continue;
            }

            // First positional arg — check if it's a known plugin
            if (PluginRegistry.KnownPlugins.ContainsKey(arg))
            {
                return arg;
            }

            // First positional arg that isn't a plugin (e.g. "version", "plugin")
            return null;
        }

        return null;
    }

    /// <summary>
    /// Extracts the value of <c>--plugin-path</c> directly from raw args.
    /// </summary>
    internal static string? DetectPluginPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == PluginPathFlag)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the value of <c>--plugin-version</c> directly from raw args.
    /// </summary>
    internal static string? DetectVersion(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == VersionFlag)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Eagerly loads the real <see cref="System.CommandLine.Command"/> for the
    /// named plugin. Resolution chain: <c>--plugin-path</c> → <c>--plugin-version</c>
    /// → project context → <c>null</c>.
    /// </summary>
    internal static async Task<Command?> LoadPluginCommandAsync(
        string pluginName,
        string? pluginPath,
        string? explicitVersion,
        CancellationToken ct)
    {
        try
        {
            string assemblyPath;

            if (pluginPath is not null)
            {
                assemblyPath = Path.GetFullPath(pluginPath);
            }
            else
            {
                PluginResolution? resolution = null;

                // --plugin-version takes priority (explicit user intent)
                if (explicitVersion is not null)
                {
                    resolution = ProjectContextScanner.ResolveExplicit(pluginName, explicitVersion);
                }

                // Fallback to project context (NuGet-resolved version)
                resolution ??= await ProjectContextScanner.ResolveAsync(pluginName, ct: ct);

                if (resolution is null)
                {
                    return null;
                }

                assemblyPath = await NuGetPluginResolver.EnsureDownloadedAsync(resolution, ct);
            }

            var plugin = PluginLoader.Load(assemblyPath);
            return plugin.GetCommand();
        }
        catch (InvalidOperationException ex)
        {
            await Console.Error.WriteLineAsync($"Warning: could not pre-load plugin '{pluginName}': {ex.Message}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"Warning: could not pre-load plugin '{pluginName}': {ex.Message}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            await Console.Error.WriteLineAsync($"Warning: could not pre-load plugin '{pluginName}': {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            await Console.Error.WriteLineAsync($"Warning: could not pre-load plugin '{pluginName}': {ex.Message}");
            return null;
        }
    }
}
