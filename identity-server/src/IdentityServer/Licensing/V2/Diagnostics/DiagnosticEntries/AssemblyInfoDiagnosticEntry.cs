// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class AssemblyInfoDiagnosticEntry : IDiagnosticEntry
{
    private readonly IReadOnlyList<Predicate<string>> _defaultInclusions =
    [
        x => x.StartsWith("Duende."),
        x => x == "Microsoft.AspNetCore",
        x => x.StartsWith("Microsoft.AspNetCore.Authentication."),
        x => x.StartsWith("Microsoft.IdentityModel."),
        x => x.StartsWith("System.IdentityModel."),
        x => x.StartsWith("System.IdentityModel"),
        x => x.StartsWith("Microsoft.EntityFrameworkCore"),
        x => x.StartsWith("Rsk"),
        x => x.StartsWith("Skoruba.IdentityServer"),
        x => x.StartsWith("Skoruba.Duende"),
        x => x.StartsWith("Npgsql"),
        x => x.StartsWith("Azure"),
        x => x.StartsWith("Microsoft.Azure")
    ];

    private readonly IReadOnlyList<Predicate<string>> _inclusions;

    public AssemblyInfoDiagnosticEntry(IReadOnlyList<Predicate<string>> inclusions = null) => _inclusions = inclusions ?? _defaultInclusions;

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        var assemblies = GetAssemblyInfo();
        writer.WriteStartObject("AssemblyInfo");
        writer.WriteString("DotnetVersion", RuntimeInformation.FrameworkDescription);
        writer.WriteNumber("AssemblyCount", assemblies.Count);

        writer.WriteStartArray("Assemblies");
        foreach (var assembly in assemblies.Where(assembly => _inclusions.Any(predicate => predicate(assembly.FullName))))
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
