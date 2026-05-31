// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Duende.Storage.Schema;

namespace Duende.Storage.MsSql.Internal;

internal static class MigrationScriptLoader
{
    private static readonly Regex VersionPattern = new(@"\.Migrations\.V(\d+)_", RegexOptions.Compiled);

    public static IEnumerable<(int TargetVersion, string Sql)> GetScripts(
        Assembly assembly,
        DatabaseSchemaVersion fromVersion,
        string schemaName)
    {
        var assemblyName = assembly.GetName().Name;
        var prefix = $"{assemblyName}.Migrations.V";

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .Select(name => (Name: name, Version: ParseVersion(name)))
            .Where(x => x.Version > fromVersion.Value)
            .OrderBy(x => x.Version)
            .Select(x => (x.Version, ApplySchema(ReadResource(assembly, x.Name), schemaName)));
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

    private static string ApplySchema(string sql, string schemaName) =>
        sql.Replace("[[schemaname]]", schemaName, StringComparison.Ordinal);
}
