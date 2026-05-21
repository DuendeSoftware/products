// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;
using Duende.Cli.PluginAbstractions;

namespace Duende.Cli.TestPlugin;

/// <summary>
/// A minimal CLI plugin used for integration testing the plugin loading infrastructure.
/// </summary>
public sealed class TestCliPlugin : ICliPlugin
{
    /// <inheritdoc />
    public string Name => "test";

    /// <inheritdoc />
    public Command GetCommand()
    {
        var command = new Command("test", "A test plugin for integration testing.");
        command.Subcommands.Add(new Command("hello", "A sample subcommand."));
        return command;
    }
}
