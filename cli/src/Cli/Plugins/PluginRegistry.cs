// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Cli.Plugins;

/// <summary>
/// Information about a known CLI plugin.
/// </summary>
internal sealed record PluginInfo(string PackageId, string AnchorPackage, string Description);

/// <summary>
/// The resolved plugin package ID and version, ready for NuGet download.
/// </summary>
internal sealed record PluginResolution(string PackageId, string Version);

/// <summary>
/// Hardcoded registry of all known first-party Duende CLI plugins.
/// Maps the subcommand name to plugin NuGet package information.
/// </summary>
internal static class PluginRegistry
{
    internal static readonly IReadOnlyDictionary<string, PluginInfo> KnownPlugins =
        new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage"] = new(
                PackageId: "Duende.Storage.CliPlugin",
                AnchorPackage: "Duende.Storage",
                Description: "Duende Storage"),
        };
}
