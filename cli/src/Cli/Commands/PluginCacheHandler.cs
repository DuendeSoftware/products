// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Cli.Plugins;

using NuGet.Configuration;

namespace Duende.Cli.Commands;

/// <summary>
/// Handles the <c>duende plugin cache clear</c> command.
/// Removes cached Duende CLI plugin packages from the NuGet global packages folder.
/// </summary>
internal static class PluginCacheHandler
{
    internal static void Execute()
    {
        var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(null));
        var cleared = 0;

        foreach (var (_, info) in PluginRegistry.KnownPlugins)
        {
#pragma warning disable CA1308 // NuGet global packages folder uses lowercase package IDs by convention
            var packageDir = Path.Combine(globalPackagesFolder, info.PackageId.ToLowerInvariant());
#pragma warning restore CA1308

            if (Directory.Exists(packageDir))
            {
                try
                {
                    Directory.Delete(packageDir, recursive: true);
                    Console.WriteLine($"Cleared cache for {info.PackageId}");
                    cleared++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"Warning: could not clear cache for {info.PackageId}: {ex.Message}");
                }
            }
        }

        if (cleared == 0)
        {
            Console.WriteLine("No cached plugins found.");
        }
        else
        {
            Console.WriteLine($"Cleared {cleared} cached plugin(s).");
        }
    }
}
