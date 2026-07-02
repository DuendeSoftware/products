// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Cli.Plugins;

/// <summary>
/// Orchestrates lazy plugin loading: resolves the plugin version from project context,
/// deployed assemblies, or explicit version flag, downloads via NuGet if not cached,
/// loads via AssemblyLoadContext, and re-invokes parsing.
/// </summary>
internal static class PluginOrchestrator
{
    internal static async Task<int> LoadAndReInvokeAsync(
        string pluginName,
        string? pluginPath,
        string? explicitVersion,
        string[] originalArgs,
        CancellationToken ct)
    {
        // Phase 1: resolve the plugin assembly path
        string assemblyPath;

        if (pluginPath is not null)
        {
            // Local dev: load directly from path
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
                throw new InvalidOperationException(
                    $"Could not determine the version for plugin '{pluginName}'. " +
                    "This can happen if: no project or solution exists in the current directory, " +
                    "the project does not reference the anchor package, " +
                    "or 'dotnet restore' has not been run. " +
                    "Use --plugin-version <version> to specify the plugin version, " +
                    "or --plugin-path <path> to load a plugin directly.");
            }

            // Download from nuget.org if not already cached
            assemblyPath = await NuGetPluginResolver.EnsureDownloadedAsync(resolution, ct);
        }

        // Phase 2: load plugin via ALC
        var plugin = PluginLoader.Load(assemblyPath);

        // Phase 3: build a fresh root with the real plugin command and re-invoke
        var reInvokeRoot = new RootCommand();
        reInvokeRoot.Subcommands.Add(plugin.GetCommand());
        // InvokeAsync does not accept a CancellationToken in System.CommandLine 2.0.x
#pragma warning disable CA2016 // CancellationToken not supported by this overload
        return await reInvokeRoot.Parse(originalArgs).InvokeAsync();
#pragma warning restore CA2016
    }
}
