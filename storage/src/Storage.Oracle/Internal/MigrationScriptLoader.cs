// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Duende.Storage.Schema;

namespace Duende.Storage.Oracle.Internal;

internal static class MigrationScriptLoader
{
    private static readonly Regex VersionPattern = new(@"\.Migrations\.V(\d+)_", RegexOptions.Compiled);

    /// <summary>
    /// Loads migration scripts whose version is greater than <paramref name="fromVersion"/>.
    /// Oracle's driver executes a single statement per command and DDL cannot be combined,
    /// so each script is split into individual statements on lines containing only a slash
    /// (the SQL*Plus statement terminator).
    /// </summary>
    public static IEnumerable<(int TargetVersion, IReadOnlyList<string> Statements)> GetScripts(
        Assembly assembly,
        DatabaseSchemaVersion fromVersion,
        string schemaPrefix)
    {
        var assemblyName = assembly.GetName().Name;
        var prefix = $"{assemblyName}.Migrations.V";

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .Select(name => (Name: name, Version: ParseVersion(name)))
            .Where(x => x.Version > fromVersion.Value)
            .OrderBy(x => x.Version)
            .Select(x => (x.Version, (IReadOnlyList<string>)SplitStatements(ApplySchema(ReadResource(assembly, x.Name), schemaPrefix))));
    }

    private static int ParseVersion(string resourceName)
    {
        var match = VersionPattern.Match(resourceName);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ApplySchema(string sql, string schemaPrefix) =>
        sql.Replace("[[schema]]", schemaPrefix, StringComparison.Ordinal);

    private static List<string> SplitStatements(string sql)
    {
        var statements = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var rawLine in sql.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Trim() == "/")
            {
                AppendStatement(statements, current);
                _ = current.Clear();
                continue;
            }

            _ = current.Append(line).Append('\n');
        }

        AppendStatement(statements, current);
        return statements;
    }

    private static void AppendStatement(List<string> statements, System.Text.StringBuilder builder)
    {
        var statement = builder.ToString().Trim();
        if (statement.Length > 0)
        {
            statements.Add(statement);
        }
    }
}
