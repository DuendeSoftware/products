// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.InteropServices;
using System.Text.Json;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class AssemblyInfoDiagnosticEntryTests
{
    [Fact]
    public async Task Should_Write_Assembly_Info()
    {
        var subject = new AssemblyInfoDiagnosticEntry();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var assemblyInfo = result.RootElement.GetProperty("AssemblyInfo");
        assemblyInfo.GetProperty("AssemblyCount").ValueKind.ShouldBe(JsonValueKind.Number);
        var assemblies = assemblyInfo.GetProperty("Assemblies");
        assemblies.ValueKind.ShouldBe(JsonValueKind.Array);
        var firstEntry = assemblies.EnumerateArray().First();
        firstEntry.GetProperty("Name").ValueKind.ShouldBe(JsonValueKind.String);
        firstEntry.GetProperty("Version").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Should_Honor_Assembly_Filter()
    {
        var subject = new AssemblyInfoDiagnosticEntry([x => x.StartsWith("Duende.")]);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var assemblyInfo = result.RootElement.GetProperty("AssemblyInfo");
        var assemblies = assemblyInfo.GetProperty("Assemblies").EnumerateArray();
        assemblies.ShouldAllBe(x => x.GetProperty("Name").GetString().StartsWith("Duende."));
    }

    [Fact]
    public async Task Should_Include_Dotnet_Version()
    {
        var subject = new AssemblyInfoDiagnosticEntry();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var assemblyInfo = result.RootElement.GetProperty("AssemblyInfo");
        assemblyInfo.GetProperty("DotnetVersion").GetString().ShouldBe(RuntimeInformation.FrameworkDescription);
    }
}
