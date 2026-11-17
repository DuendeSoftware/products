// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Duende.Storage.Schema;

namespace Duende.Storage.CliPlugin.Commands;

internal static partial class StorageCommandOutput
{
    [GeneratedRegex(@"(password|pwd|user id|uid|username|userid|host|server|data source|datasource|database|port)\s*=\s*([^;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveConnectionStringKeys();

    internal static string SanitizeMessage(string message) =>
        SensitiveConnectionStringKeys().Replace(message, "$1=[redacted]");

    internal static async Task WriteVerificationErrorsAsync(SchemaVerificationResult result, TextWriter err)
    {
        await err.WriteLineAsync("Schema verification found the following problems:");
        foreach (var error in result.Errors)
        {
            var message = error.ErrorMessage.Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            await err.WriteLineAsync(message);
        }
    }
}
