// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace Duende.Bff.Diagnostics.DiagnosticEntries;

internal class AssemblyInfoDiagnosticEntry : IDiagnosticEntry
{
    private readonly IReadOnlyList<string> _exactMatches =
    [
        "Microsoft.AspNetCore"
    ];

    private readonly IReadOnlyList<string> _startsWithMatches =
    [
        "Duende.",
        "Microsoft.AspNetCore.Components.",
        "Microsoft.AspNetCore.Authentication.",
        "Microsoft.IdentityModel.",
        "System.IdentityModel.",
        "System.IdentityModel",
        "Microsoft.EntityFrameworkCore",
    ];

    public void Write(DiagnosticContext _, Utf8JsonWriter writer)
    {
        var assemblies = GetAssemblyInfo();
        writer.WriteStartObject("AssemblyInfo");
        writer.WriteString("DotNetVersion", RuntimeInformation.FrameworkDescription);
        writer.WriteString("BFF",
            typeof(DiagnosticHostedService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion);

        writer.WriteStartArray("Assemblies");
        foreach (var assembly in assemblies.Where(assembly => assembly.GetName().Name != null &&
                                                              (_exactMatches.Contains(assembly.GetName().Name) ||
                                                               _startsWithMatches.Any(prefix =>
                                                                   assembly.GetName().Name!.StartsWith(prefix,
                                                                       StringComparison.Ordinal)))))
        {
            writer.WriteStartObject();
            writer.WriteString("Name", assembly.GetName().Name);
            writer.WriteString("Version", assembly.GetName().Version?.ToString() ?? "Unknown");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static List<Assembly> GetAssemblyInfo()
    {
        var assemblies = AssemblyLoadContext.Default.Assemblies
            .OrderBy(a => a.FullName)
            .ToList();

        return assemblies;
    }
}
