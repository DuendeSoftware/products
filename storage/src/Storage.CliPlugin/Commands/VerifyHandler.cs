// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Duende.Storage.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.CliPlugin.Commands;

internal static class VerifyHandler
{
    internal static Task<int> RunAsync(string provider, string connectionString, string? schemaName, Ct ct) =>
        RunAsync(provider, connectionString, schemaName, Console.Out, Console.Error, ct);

    internal static async Task<int> RunAsync(
        string provider,
        string connectionString,
        string? schemaName,
        TextWriter @out,
        TextWriter err,
        Ct ct)
    {
        ArgumentNullException.ThrowIfNull(@out);
        ArgumentNullException.ThrowIfNull(err);

        try
        {
            await @out.WriteLineAsync($"Connecting to {provider} database...");

            var effectiveConnectionString = GetReadOnlyConnectionString(provider, connectionString);
            await using var serviceProvider =
                DatabaseProviderFactory.CreateServiceProvider(provider, effectiveConnectionString, schemaName);
            var storageInstanceSchema = serviceProvider.GetRequiredService<IStorageInstanceSchema>();
            var version = await storageInstanceSchema.CheckVersionAsync(ct);

            await @out.WriteLineAsync($"Current schema version: {version.CurrentVersion}");
            await @out.WriteLineAsync($"Required schema version: {version.RequiredVersion}");

            var verification = await storageInstanceSchema.VerifySchemaAsync(ct);
            if (!verification.IsValid)
            {
                await StorageCommandOutput.WriteVerificationErrorsAsync(verification, err);
            }

            if (version.CurrentVersion > version.RequiredVersion)
            {
                await err.WriteLineAsync(
                    $"The database schema (version {version.CurrentVersion}) is newer than this tool supports (version {version.RequiredVersion}). Update the Duende CLI and Storage packages.");
                return 1;
            }

            if (version.CurrentVersion < version.RequiredVersion)
            {
                await @out.WriteLineAsync("Schema migration is pending. Run 'duende storage migrate' to update the schema.");
                return 2;
            }

            if (!verification.IsValid)
            {
                return 1;
            }

            await @out.WriteLineAsync("Schema verification succeeded.");
            return 0;
        }
        catch (DbException)
        {
            await err.WriteLineAsync(
                "Failed to access the database or read its schema. Verify your connection string and database permissions.");
            return 1;
        }
        catch (ArgumentException ex)
        {
            await err.WriteLineAsync(
                $"Invalid connection string or provider configuration. Verify your --connection-string value.{Environment.NewLine}{StorageCommandOutput.SanitizeMessage(ex.Message)}");
            return 1;
        }
        catch (ValidationException ex)
        {
            await err.WriteLineAsync($"Invalid provider configuration: {StorageCommandOutput.SanitizeMessage(ex.Message)}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            await err.WriteLineAsync($"Schema verification failed: {StorageCommandOutput.SanitizeMessage(ex.Message)}");
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
                $"An unexpected error occurred: {StorageCommandOutput.SanitizeMessage(ex.Message)}{Environment.NewLine}Please file an issue at https://github.com/DuendeSoftware/products-private");
            return 1;
        }
    }

    internal static string GetReadOnlyConnectionString(string provider, string connectionString)
    {
        if (provider != "sqlite")
        {
            return connectionString;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var isUri = builder.DataSource.StartsWith("file:", StringComparison.Ordinal);
        if (isUri)
        {
            var fragmentStart = builder.DataSource.IndexOf('#', StringComparison.Ordinal);
            var source = fragmentStart < 0 ? builder.DataSource : builder.DataSource[..fragmentStart];
            var fragment = fragmentStart < 0 ? "" : builder.DataSource[fragmentStart..];
            var queryStart = source.IndexOf('?', StringComparison.Ordinal);
            var path = queryStart < 0 ? source : source[..queryStart];
            var parameters = queryStart < 0 ? [] : source[(queryStart + 1)..].Split('&').ToList();
            var hasMode = false;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parts = parameters[i].Split('=', 2);
                if (Uri.UnescapeDataString(parts[0]) != "mode")
                {
                    continue;
                }

                hasMode = true;
                if (parts.Length == 2 && Uri.UnescapeDataString(parts[1]) is "rw" or "rwc")
                {
                    parameters[i] = "mode=ro";
                }
            }

            if (!hasMode && builder.Mode == SqliteOpenMode.Memory)
            {
                parameters.Add("mode=memory");
            }

            builder.DataSource = path + (parameters.Count == 0 ? "" : "?" + string.Join('&', parameters)) + fragment;
        }

        // URI modes can override Mode=Memory, so every URI must use read-only open flags.
        if (isUri || builder.Mode != SqliteOpenMode.Memory &&
            !string.Equals(builder.DataSource, ":memory:", StringComparison.Ordinal))
        {
            builder.Mode = SqliteOpenMode.ReadOnly;
        }

        return builder.ToString();
    }
}
