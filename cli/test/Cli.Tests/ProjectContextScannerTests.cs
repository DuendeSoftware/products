// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Cli.Plugins;

namespace Duende.Cli.Tests;

public class ProjectContextScannerTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectContextScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CliTests_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_finds_anchor_version_from_Directory_Packages_props()
    {
        var propsContent = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Duende.Storage" Version="7.2.1" />
              </ItemGroup>
            </Project>
            """;
        File.WriteAllText(Path.Combine(_tempDir, "Directory.Packages.props"), propsContent);

        var result = ProjectContextScanner.Resolve("storage", _tempDir);

        var resolved = result.ShouldNotBeNull();
        resolved.PackageId.ShouldBe("Duende.Storage.CliPlugin");
        resolved.Version.ShouldBe("7.2.*");
    }

    [Fact]
    public void Resolve_returns_null_when_no_anchor_package_exists()
    {
        var propsContent = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="SomeOtherPackage" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;
        File.WriteAllText(Path.Combine(_tempDir, "Directory.Packages.props"), propsContent);

        var result = ProjectContextScanner.Resolve("storage", _tempDir);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_returns_null_for_unknown_plugin_name()
    {
        var result = ProjectContextScanner.Resolve("nonexistent", _tempDir);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_finds_anchor_version_from_csproj()
    {
        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Duende.Storage" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """;
        File.WriteAllText(Path.Combine(_tempDir, "Test.csproj"), csprojContent);

        var result = ProjectContextScanner.Resolve("storage", _tempDir);

        var resolved = result.ShouldNotBeNull();
        resolved.PackageId.ShouldBe("Duende.Storage.CliPlugin");
        resolved.Version.ShouldBe("8.0.*");
    }

    [Fact]
    public void Resolve_finds_version_from_deployed_assembly()
    {
        // Copy the Storage.CliPlugin assembly as a stand-in for "Duende.Storage.dll"
        // We need a real .NET assembly with version info — use the test assembly itself
        var sourceAssembly = typeof(ProjectContextScannerTests).Assembly.Location;
        var anchorPath = Path.Combine(_tempDir, "Duende.Storage.dll");
        File.Copy(sourceAssembly, anchorPath);

        // The test assembly won't have the right version, but we can verify the scanner
        // finds *something* from the deployed assembly (non-null result)
        var result = ProjectContextScanner.Resolve("storage", _tempDir);

        // Should resolve — the assembly has version info even if it's not "7.2.1"
        var resolved = result.ShouldNotBeNull();
        resolved.PackageId.ShouldBe("Duende.Storage.CliPlugin");
    }

    [Fact]
    public void Resolve_prefers_project_context_over_deployed_assembly()
    {
        // Set up both project context and deployed assembly
        var propsContent = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Duende.Storage" Version="7.2.1" />
              </ItemGroup>
            </Project>
            """;
        File.WriteAllText(Path.Combine(_tempDir, "Directory.Packages.props"), propsContent);

        var sourceAssembly = typeof(ProjectContextScannerTests).Assembly.Location;
        File.Copy(sourceAssembly, Path.Combine(_tempDir, "Duende.Storage.dll"));

        var result = ProjectContextScanner.Resolve("storage", _tempDir);

        // Should use project context version, not assembly version
        var resolved = result.ShouldNotBeNull();
        resolved.Version.ShouldBe("7.2.*");
    }

    [Fact]
    public void ResolveExplicit_creates_resolution_from_version_string()
    {
        var result = ProjectContextScanner.ResolveExplicit("storage", "7.2.1");

        var resolved = result.ShouldNotBeNull();
        resolved.PackageId.ShouldBe("Duende.Storage.CliPlugin");
        resolved.Version.ShouldBe("7.2.*");
    }

    [Fact]
    public void ResolveExplicit_returns_null_for_unknown_plugin()
    {
        var result = ProjectContextScanner.ResolveExplicit("nonexistent", "1.0.0");

        result.ShouldBeNull();
    }
}
