// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Cli.PluginAbstractions;

/// <summary>
/// Provides host services to CLI plugins at runtime.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Gets a logger for the plugin to use.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Gets the version of the <c>duende</c> CLI tool host.
    /// </summary>
    string HostVersion { get; }

    /// <summary>
    /// Gets a cancellation token that is cancelled when the CLI is shutting down.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
