// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;
using System.Reflection;
using Duende.Cli.Commands;
using Duende.Cli.Plugins;

var rootCommand = new RootCommand("Duende CLI — tooling for Duende Software products.");

// Built-in: version
var versionCommand = new Command("version", "Show the Duende CLI version.");
versionCommand.SetAction(_ =>
{
    var version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown";
    Console.WriteLine($"duende {version}");
});
rootCommand.Subcommands.Add(versionCommand);

// Built-in: plugin
var pluginCommand = new Command("plugin", "Manage CLI plugins.");
rootCommand.Subcommands.Add(pluginCommand);

// plugin list
var pluginListCommand = new Command("list", "List known plugins and their resolved versions.");
pluginListCommand.SetAction(_ => PluginListHandler.Execute());
pluginCommand.Subcommands.Add(pluginListCommand);

// plugin cache
var pluginCacheCommand = new Command("cache", "Manage the plugin cache.");
pluginCommand.Subcommands.Add(pluginCacheCommand);

var pluginCacheClearCommand = new Command("clear", "Clear the NuGet global packages cache entries for Duende plugins.");
pluginCacheClearCommand.SetAction(_ => PluginCacheHandler.Execute());
pluginCacheCommand.Subcommands.Add(pluginCacheClearCommand);

// --plugin-path option (for local development — bypasses NuGet resolution)
var pluginPathOption = new Option<string?>("--plugin-path")
{
    Description = "Load a plugin directly from a DLL path, bypassing NuGet resolution.",
};
rootCommand.Options.Add(pluginPathOption);

// --plugin-version option (explicit plugin version when no project context or deployed assembly is available)
var pluginVersionOption = new Option<string?>("--plugin-version")
{
    Description = "Specify the plugin version to download (e.g. 7.2.1). Used when no project context or deployed assembly is found.",
};
rootCommand.Options.Add(pluginVersionOption);

// Detect which plugin (if any) the user is invoking so we can register it before parsing.
// This ensures `duende storage --help` shows the real subcommands, not just the stub.
var requestedPlugin = PluginPreloader.DetectRequestedPlugin(args);
var pluginPath = PluginPreloader.DetectPluginPath(args);
var explicitVersion = PluginPreloader.DetectVersion(args);

if (requestedPlugin is not null)
{
    // Eagerly load the real plugin command and register it in place of the stub
    var realCommand = await PluginPreloader.LoadPluginCommandAsync(
        requestedPlugin, pluginPath, explicitVersion, CancellationToken.None);
    if (realCommand is not null)
    {
        rootCommand.Subcommands.Add(realCommand);
    }
    else
    {
        // Fallback to stub if loading fails (e.g. no project context, no --plugin-path, no --plugin-version)
        PluginStubRegistrar.Register(rootCommand, pluginPathOption, pluginVersionOption, args, onlyPlugin: requestedPlugin);
    }

    // Register stubs for all other plugins
    PluginStubRegistrar.Register(rootCommand, pluginPathOption, pluginVersionOption, args, excludePlugin: requestedPlugin);
}
else
{
    // No specific plugin invoked — register stubs for all known plugins
    PluginStubRegistrar.Register(rootCommand, pluginPathOption, pluginVersionOption, args);
}

return await rootCommand.Parse(args).InvokeAsync();
