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
        command.SetAction(async (_, ct) =>
        {
            Console.WriteLine("Storage migrate: not yet implemented.");
            await Task.CompletedTask;
        });
        return command;
    }
}
