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
        var assemblies = assemblyInfo.GetProperty("Assemblies");
        assemblies.ValueKind.ShouldBe(JsonValueKind.Array);
        var firstEntry = assemblies.EnumerateArray().First();
        firstEntry.GetProperty("Name").ValueKind.ShouldBe(JsonValueKind.String);
        firstEntry.GetProperty("Version").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Should_Honor_Assembly_Exact_Matches()
    {
        var subject = new AssemblyInfoDiagnosticEntry(["Duende.IdentityServer"], []);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var assemblyInfo = result.RootElement.GetProperty("AssemblyInfo");
        var assemblies = assemblyInfo.GetProperty("Assemblies").EnumerateArray();
        assemblies.ShouldHaveSingleItem();
        var firstEntry = assemblies.First();
        firstEntry.GetProperty("Name").GetString().ShouldBe("Duende.IdentityServer");
    }

    [Fact]
    public async Task Should_Honor_Assembly_StartsWith_Matches()
    {
        var subject = new AssemblyInfoDiagnosticEntry([], ["Duende."]);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var assemblyInfo = result.RootElement.GetProperty("AssemblyInfo");
        var assemblies = assemblyInfo.GetProperty("Assemblies").EnumerateArray();
        assemblies.ShouldAllBe(assembly =>
            assembly.GetProperty("Name").GetString().StartsWith("Duende."));
    }

    [Fact]
    public async Task Should_Include_Only_Matching_Assemblies()
    {
        var subject = new AssemblyInfoDiagnosticEntry(["Microsoft.AspnetCore"], ["Duende."]);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var assemblyInfo = result.RootElement.GetProperty("AssemblyInfo");
        var assemblies = assemblyInfo.GetProperty("Assemblies").EnumerateArray();
        assemblies.ShouldAllBe(assembly =>
            assembly.GetProperty("Name").GetString() == "Microsoft.AspnetCore" ||
            assembly.GetProperty("Name").GetString().StartsWith("Duende."));
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
