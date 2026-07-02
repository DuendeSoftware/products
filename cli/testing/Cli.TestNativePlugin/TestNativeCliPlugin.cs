// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;
using Duende.Cli.PluginAbstractions;
using Microsoft.Data.Sqlite;

namespace Duende.Cli.TestNativePlugin;

/// <summary>
/// A minimal CLI plugin with native dependencies, used for integration testing
/// that the isolated <see cref="System.Runtime.Loader.AssemblyLoadContext"/> correctly
/// resolves unmanaged (native) DLLs via <c>LoadUnmanagedDll</c>.
/// </summary>
public sealed class TestNativeCliPlugin : ICliPlugin
{
    /// <inheritdoc />
    public string Name => "test-native";

    /// <inheritdoc />
    public Command GetCommand()
    {
        var command = new Command("test-native", "Verifies native DLL loading in an isolated AssemblyLoadContext.");
        command.SetAction(async (_, ct) =>
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(ct);
            return 0;
        });
        return command;
    }
}
