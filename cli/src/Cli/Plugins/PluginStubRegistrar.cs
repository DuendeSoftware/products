// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Cli.Plugins;

/// <summary>
/// Registers stub commands for known plugins onto the root command.
/// Each stub command lazily downloads and loads the real plugin on first invocation.
/// </summary>
internal static class PluginStubRegistrar
{
    /// <summary>Registers stubs for all known plugins.</summary>
    internal static void Register(
        RootCommand rootCommand,
        Option<string?> pluginPathOption,
        Option<string?> pluginVersionOption,
        string[] originalArgs) =>
        Register(rootCommand, pluginPathOption, pluginVersionOption, originalArgs, onlyPlugin: null, excludePlugin: null);

    /// <summary>
    /// Registers stubs for known plugins, filtered by <paramref name="onlyPlugin"/>
    /// or <paramref name="excludePlugin"/>. Pass <c>onlyPlugin</c> to register a
    /// single stub; pass <c>excludePlugin</c> to register all except one.
    /// </summary>
    internal static void Register(
        RootCommand rootCommand,
        Option<string?> pluginPathOption,
        Option<string?> pluginVersionOption,
        string[] originalArgs,
        string? onlyPlugin = null,
        string? excludePlugin = null)
    {
        foreach (var (name, info) in PluginRegistry.KnownPlugins)
        {
            if (onlyPlugin is not null && !string.Equals(name, onlyPlugin, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (excludePlugin is not null && string.Equals(name, excludePlugin, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stubCommand = new Command(name, $"Commands from the {info.Description} plugin. (Downloads on first use.)");
            var pluginNameCapture = name;

            stubCommand.SetAction(async (parseResult, ct) =>
            {
                var pluginPath = parseResult.GetValue(pluginPathOption);
                var explicitVersion = parseResult.GetValue(pluginVersionOption);
                Environment.ExitCode = await PluginOrchestrator.LoadAndReInvokeAsync(pluginNameCapture, pluginPath, explicitVersion, originalArgs, ct);
            });

            rootCommand.Subcommands.Add(stubCommand);
        }
    }
}
