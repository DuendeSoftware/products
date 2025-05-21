// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class AssemblyInfoDiagnosticEntry : IDiagnosticEntry
{
    public Task WriteAsync(Utf8JsonWriter writer)
    {
        var assemblies = GetAssemblyInfo();
        writer.WriteStartObject("AssemblyInfo");
        writer.WriteNumber("AssemblyCount", assemblies.Count);

        writer.WriteStartArray("Assemblies");
        foreach (var assembly in assemblies)
        {
            writer.WriteStartObject();
            writer.WriteString("Name", assembly.GetName().Name);
            writer.WriteString("Version", assembly.GetName().Version?.ToString() ?? "Unknown");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private List<Assembly> GetAssemblyInfo()
    {
        var assemblies = AssemblyLoadContext.Default.Assemblies
            .OrderBy(a => a.FullName)
            .ToList();

        return assemblies;
    }
}
