// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Cli.Plugins;

namespace Duende.Cli.Commands;

/// <summary>
/// Handles the <c>duende plugin list</c> command.
/// Lists all known plugins and their resolved versions from project context.
/// </summary>
internal static class PluginListHandler
{
    internal static async Task ExecuteAsync(CancellationToken ct)
    {
        Console.WriteLine("Known plugins:");
        Console.WriteLine();
        Console.WriteLine($"  {"Name",-15} {"Package",-35} {"Version",-15} {"Status"}");
        Console.WriteLine($"  {"----",-15} {"-------",-35} {"-------",-15} {"------"}");

        foreach (var (name, info) in PluginRegistry.KnownPlugins)
        {
            var resolution = await ProjectContextScanner.ResolveAsync(name, ct: ct);
            var version = resolution?.Version ?? "(not detected)";
            var status = resolution is not null ? "resolved" : "not resolved";

            Console.WriteLine($"  {name,-15} {info.PackageId,-35} {version,-15} {status}");
        }

        Console.WriteLine();
        Console.WriteLine("Tip: Run from a project directory to auto-detect plugin versions.");
    }
}
