// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Cli.PluginAbstractions;

/// <summary>
/// Defines the contract for a CLI plugin that contributes commands to the <c>duende</c> tool.
/// </summary>
public interface ICliPlugin
{
    /// <summary>
    /// Gets the name of the plugin (e.g. "storage"). Used as the subcommand name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the <see cref="Command"/> that this plugin contributes to the CLI root command.
    /// The returned command and all its subcommands are mounted directly onto the root.
    /// </summary>
    Command GetCommand();
}
