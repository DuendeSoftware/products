// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Storage.CliPlugin.Commands;

/// <summary>
/// Provides the <c>duende storage migrate</c> command.
/// </summary>
internal static class MigrateCommand
{
    private const string ConnectionStringEnvironmentVariable = "DUENDE_STORAGE_CONNECTION_STRING";

    private static readonly string[] SupportedProviders = ["postgresql", "mssql", "sqlite"];

    internal static Command Create()
    {
        var command = new Command("migrate", "Apply pending Duende Storage schema migrations.");
        var providerOption = new Option<string>("--provider")
        {
            Description = "Database provider to migrate: postgresql, mssql, sqlite.",
            Required = true,
        };

        var connectionStringOption = new Option<string?>("--connection-string")
        {
            Description = $"Database connection string. Falls back to {ConnectionStringEnvironmentVariable}.",
        };
        var schemaOption = new Option<string?>("--schema")
        {
            Description =
                "Database schema name. Defaults to public for postgresql and dbo for mssql. sqlite does not support schemas.",
        };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Show migration SQL without executing it.",
        };

        command.Add(providerOption);
        command.Add(connectionStringOption);
        command.Add(schemaOption);
        command.Add(dryRunOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var rawProvider = parseResult.GetValue(providerOption)!;

            if (!SupportedProviders.Contains(rawProvider, StringComparer.OrdinalIgnoreCase))
            {
                await Console.Error.WriteLineAsync(
                    $"Unknown provider '{rawProvider}'. Supported providers: postgresql, mssql, sqlite.");
                return 1;
            }

#pragma warning disable CA1308
            // Provider names are canonical lowercase identifiers, not security-sensitive
            var provider = rawProvider.ToLowerInvariant();
#pragma warning restore CA1308

            var connectionString = parseResult.GetValue(connectionStringOption) ??
                                   Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                await Console.Error.WriteLineAsync(
                    $"A connection string is required. Specify --connection-string or set {ConnectionStringEnvironmentVariable}.");
                return 1;
            }

            var schemaName = parseResult.GetValue(schemaOption);
            if (provider == "sqlite" && !string.IsNullOrWhiteSpace(schemaName))
            {
                await Console.Error.WriteLineAsync("sqlite does not support database schemas.");
                return 1;
            }

            var effectiveSchemaName = GetSchemaName(provider, schemaName);
            return await MigrateHandler.RunAsync(provider, connectionString, effectiveSchemaName,
                parseResult.GetValue(dryRunOption), ct);
        });

        return command;
    }

    private static string? GetSchemaName(string provider, string? schemaName) =>
        string.IsNullOrWhiteSpace(schemaName)
            ? provider switch
            {
                "postgresql" => "public",
                "mssql" => "dbo",
                _ => null,
            }
            : schemaName;
}
