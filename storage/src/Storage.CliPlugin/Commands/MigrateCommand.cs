// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Storage.CliPlugin.Commands;

/// <summary>
/// Provides the <c>duende storage migrate</c> command.
/// </summary>
internal static class MigrateCommand
{
    internal static Command Create()
    {
        var command = new Command("migrate", "Apply pending Duende Storage schema migrations.");
        var options = new StorageCommandOptions(command, "migrate");
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Show migration SQL without executing it.",
        };

        command.Add(dryRunOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var arguments = await options.ResolveAsync(parseResult);
            if (arguments is null)
            {
                return 1;
            }

            return await MigrateHandler.RunAsync(
                arguments.Value.Provider, arguments.Value.ConnectionString, arguments.Value.SchemaName,
                parseResult.GetValue(dryRunOption), ct);
        });

        return command;
    }
}
