// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;
using Duende.Cli.PluginAbstractions;
using Duende.Storage.CliPlugin.Commands;

namespace Duende.Storage.CliPlugin;

/// <summary>
/// Entry point for the Duende Storage CLI plugin.
/// Provides the <c>storage</c> subcommand tree to the <c>duende</c> CLI host.
/// </summary>
public sealed class StorageCliPlugin : ICliPlugin
{
    /// <inheritdoc />
    public string Name => "storage";

    /// <inheritdoc />
    public Command GetCommand()
    {
        var storageCommand = new Command("storage", "Commands for managing Duende Storage.");
        storageCommand.Subcommands.Add(MigrateCommand.Create());
        return storageCommand;
    }
}
