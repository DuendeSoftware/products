// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Storage.CliPlugin.Commands;

/// <summary>
/// Provides the <c>duende storage verify</c> command.
/// </summary>
internal static class VerifyCommand
{
    internal static Command Create()
    {
        var command = new Command("verify", "Check the Duende Storage schema without changing the database.");
        var options = new StorageCommandOptions(command, "verify");

        command.SetAction(async (parseResult, ct) =>
        {
            var arguments = await options.ResolveAsync(parseResult);
            if (arguments is null)
            {
                return 1;
            }

            return await VerifyHandler.RunAsync(
                arguments.Value.Provider, arguments.Value.ConnectionString, arguments.Value.SchemaName, ct);
        });

        return command;
    }
}
