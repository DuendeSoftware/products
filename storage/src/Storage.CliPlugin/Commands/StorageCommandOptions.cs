// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Storage.CliPlugin.Commands;

internal sealed class StorageCommandOptions
{
    private const string ConnectionStringEnvironmentVariable = "DUENDE_STORAGE_CONNECTION_STRING";
    private static readonly string[] SupportedProviders = ["postgresql", "mssql", "oracle", "sqlite"];

    private readonly Option<string> _provider;
    private readonly Option<string?> _connectionString;
    private readonly Option<string?> _schema;

    internal StorageCommandOptions(Command command, string operation)
    {
        _provider = new Option<string>("--provider")
        {
            Description = $"Database provider to {operation}: postgresql, mssql, oracle, sqlite.",
            Required = true,
        };
        _connectionString = new Option<string?>("--connection-string")
        {
            Description = $"Database connection string. Falls back to {ConnectionStringEnvironmentVariable}.",
        };
        _schema = new Option<string?>("--schema")
        {
            Description =
                "Database schema name. Defaults to public for postgresql, dbo for mssql and the connecting user's schema for oracle. sqlite does not support schemas.",
        };

        command.Add(_provider);
        command.Add(_connectionString);
        command.Add(_schema);
    }

    internal async Task<(string Provider, string ConnectionString, string? SchemaName)?> ResolveAsync(ParseResult parseResult)
    {
        var rawProvider = parseResult.GetValue(_provider)!;
        if (!SupportedProviders.Contains(rawProvider, StringComparer.OrdinalIgnoreCase))
        {
            await Console.Error.WriteLineAsync(
                $"Unknown provider '{rawProvider}'. Supported providers: postgresql, mssql, oracle, sqlite.");
            return null;
        }

#pragma warning disable CA1308
        // Provider names are canonical lowercase identifiers, not security-sensitive
        var provider = rawProvider.ToLowerInvariant();
#pragma warning restore CA1308

        var connectionString = parseResult.GetValue(_connectionString) ??
                               Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync(
                $"A connection string is required. Specify --connection-string or set {ConnectionStringEnvironmentVariable}.");
            return null;
        }

        var schemaName = parseResult.GetValue(_schema);
        if (provider == "sqlite" && !string.IsNullOrWhiteSpace(schemaName))
        {
            await Console.Error.WriteLineAsync("sqlite does not support database schemas.");
            return null;
        }

        var effectiveSchemaName = string.IsNullOrWhiteSpace(schemaName)
            ? provider switch
            {
                "postgresql" => "public",
                "mssql" => "dbo",
                _ => null,
            }
            : schemaName;

        return (provider, connectionString, effectiveSchemaName);
    }
}
