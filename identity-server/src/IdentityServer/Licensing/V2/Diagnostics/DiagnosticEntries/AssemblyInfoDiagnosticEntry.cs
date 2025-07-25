// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class AssemblyInfoDiagnosticEntry : IDiagnosticEntry
{
    private readonly IReadOnlyList<string> _defaultExactMatches =
    [
        "Microsoft.AspNetCore"
    ];
    private readonly IReadOnlyList<string> _defaultStartsWithMatches =
    [
        "Duende.",
        "Microsoft.AspNetCore.Authentication.",
        "Microsoft.IdentityModel.",
        "System.IdentityModel.",
        "System.IdentityModel",
        "Microsoft.EntityFrameworkCore",
        "Rsk",
        "Skoruba.IdentityServer",
        "Skoruba.Duende",
        "Npgsql",
        "Azure",
        "Microsoft.Azure"
    ];

    private readonly IReadOnlyList<string> _exactMatches;
    private readonly IReadOnlyList<string> _startsWithMatches;

    public AssemblyInfoDiagnosticEntry(IReadOnlyList<string> exactMatches = null, IReadOnlyList<string> startsWithMatches = null)
    {
        _exactMatches = exactMatches ?? _defaultExactMatches;
        _startsWithMatches = startsWithMatches ?? _defaultStartsWithMatches;
    }

    public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
    {
        var assemblies = GetAssemblyInfo();
        writer.WriteStartObject("AssemblyInfo");
        writer.WriteString("DotnetVersion", RuntimeInformation.FrameworkDescription);
        writer.WriteString("IdentityServerVersion", typeof(AssemblyInfoDiagnosticEntry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);

        writer.WriteStartArray("Assemblies");
        foreach (var assembly in assemblies.Where(assembly => assembly.GetName().Name != null &&
            (_exactMatches.Contains(assembly.GetName().Name) ||
             _startsWithMatches.Any(prefix => assembly.GetName().Name!.StartsWith(prefix, StringComparison.Ordinal)))))
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

    private static List<Assembly> GetAssemblyInfo()
    {
        var assemblies = AssemblyLoadContext.Default.Assemblies
            .OrderBy(a => a.FullName)
            .ToList();

        return assemblies;
    }
}
