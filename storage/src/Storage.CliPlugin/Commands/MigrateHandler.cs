// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using System.Text.RegularExpressions;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.CliPlugin.Commands;

internal static partial class MigrateHandler
{
    [GeneratedRegex(@"(password|pwd|user id|uid|username|userid|host|server|data source|datasource|database|port)\s*=\s*([^;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveConnectionStringKeys();

    internal static Task<int> RunAsync(string provider, string connectionString, string? schemaName, bool dryRun,
        Ct ct) =>
        RunAsync(provider, connectionString, schemaName, dryRun, Console.Out, Console.Error, ct);

    internal static async Task<int> RunAsync(
        string provider,
        string connectionString,
        string? schemaName,
        bool dryRun,
        TextWriter @out,
        TextWriter err,
        Ct ct)
    {
        ArgumentNullException.ThrowIfNull(@out);
        ArgumentNullException.ThrowIfNull(err);

        try
        {
            await @out.WriteLineAsync($"Connecting to {provider} database...");

            await using var serviceProvider =
                DatabaseProviderFactory.CreateServiceProvider(provider, connectionString, schemaName);
            var schema = serviceProvider.GetRequiredService<IDatabaseSchema>();
            var result = await schema.CheckVersionAsync(ct);

            await @out.WriteLineAsync($"Current schema version: {result.CurrentVersion}");
            await @out.WriteLineAsync($"Required schema version: {result.RequiredVersion}");

            if (result.CurrentVersion > result.RequiredVersion)
            {
                await err.WriteLineAsync(
                    $"The database schema (version {result.CurrentVersion}) is newer than this tool supports (version {result.RequiredVersion}). Update the Duende CLI and Storage packages.");
                return 1;
            }

            if (result.IsCompatible)
            {
                await @out.WriteLineAsync($"Schema is already up to date (version {result.CurrentVersion}).");
                return 0;
            }

            if (dryRun)
            {
                var sql = schema.BuildMigrationScript(new DatabaseSchemaVersion((int)result.CurrentVersion));
                await @out.WriteLineAsync(sql);
                await @out.WriteLineAsync("Dry run complete. No changes were made.");
                return 0;
            }

            await @out.WriteLineAsync(
                $"Migrating from version {result.CurrentVersion} to version {result.RequiredVersion}...");
            await schema.MigrateAsync(ct);
            await @out.WriteLineAsync($"Migration complete. Schema is now at version {result.RequiredVersion}.");
            return 0;
        }
        catch (DbException ex)
        {
            await err.WriteLineAsync(
                $"Failed to connect to the database. Verify your connection string and that the server is reachable.{Environment.NewLine}{SanitizeMessage(ex.Message)}");
            return 1;
        }
        catch (ArgumentException ex)
        {
            await err.WriteLineAsync(
                $"Invalid connection string or provider configuration. Verify your --connection-string value.{Environment.NewLine}{SanitizeMessage(ex.Message)}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            await err.WriteLineAsync(
                $"Migration failed: {SanitizeMessage(ex.Message)}. The database may be in a partially migrated state. Run 'duende storage migrate' again to retry.");
            return 1;
        }
        catch (OperationCanceledException)
        {
            await err.WriteLineAsync("Operation cancelled.");
            return 1;
        }
#pragma warning disable CA1031 // This is the top-level CLI entry point - catching all exceptions for user-friendly output is intentional
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await err.WriteLineAsync(
                $"An unexpected error occurred: {SanitizeMessage(ex.Message)}{Environment.NewLine}Please file an issue at https://github.com/DuendeSoftware/products-private");
            return 1;
        }
    }

    private static string SanitizeMessage(string message) =>
        SensitiveConnectionStringKeys().Replace(message, "$1=[redacted]");
}
