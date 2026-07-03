// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Cli.Plugins;

namespace Duende.Cli.Tests;

public class ProjectContextScannerTests
{
    [Fact]
    public void ParseResolvedVersion_extracts_stable_version()
    {
        var json = """
            {
              "version": 1,
              "parameters": "",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "Duende.Storage",
                          "requestedVersion": "7.2.*",
                          "resolvedVersion": "7.2.3"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBe("7.2.3");
    }

    [Fact]
    public void ParseResolvedVersion_extracts_prerelease_version()
    {
        var json = """
            {
              "version": 1,
              "parameters": "",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "Duende.Storage",
                          "requestedVersion": "1.2.0-preview.1",
                          "resolvedVersion": "1.2.0-preview.1"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBe("1.2.0-preview.1");
    }

    [Fact]
    public void ParseResolvedVersion_is_case_insensitive()
    {
        var json = """
            {
              "version": 1,
              "parameters": "",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "duende.storage",
                          "requestedVersion": "7.2.1",
                          "resolvedVersion": "7.2.1"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBe("7.2.1");
    }

    [Fact]
    public void ParseResolvedVersion_returns_null_when_package_not_found()
    {
        var json = """
            {
              "version": 1,
              "parameters": "",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "SomeOtherPackage",
                          "requestedVersion": "1.0.0",
                          "resolvedVersion": "1.0.0"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseResolvedVersion_returns_null_for_empty_json()
    {
        var result = ProjectContextScanner.ParseResolvedVersion("", "Duende.Storage");

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseResolvedVersion_returns_null_for_invalid_json()
    {
        var result = ProjectContextScanner.ParseResolvedVersion("not json", "Duende.Storage");

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseResolvedVersion_finds_package_across_multiple_projects()
    {
        var json = """
            {
              "version": 1,
              "parameters": "",
              "projects": [
                {
                  "path": "MyApp.Tests.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "xunit",
                          "requestedVersion": "2.9.3",
                          "resolvedVersion": "2.9.3"
                        }
                      ]
                    }
                  ]
                },
                {
                  "path": "MyApp.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "Duende.Storage",
                          "requestedVersion": "1.2.0-preview.1",
                          "resolvedVersion": "1.2.0-preview.1"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBe("1.2.0-preview.1");
    }

    [Fact]
    public void ParseResolvedVersion_finds_package_across_multiple_frameworks()
    {
        var json = """
            {
              "version": 1,
              "parameters": "",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net8.0",
                      "topLevelPackages": [
                        {
                          "id": "OtherPackage",
                          "requestedVersion": "1.0.0",
                          "resolvedVersion": "1.0.0"
                        }
                      ]
                    },
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "Duende.Storage",
                          "requestedVersion": "1.2.0-preview.1",
                          "resolvedVersion": "1.2.0-preview.1"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBe("1.2.0-preview.1");
    }

    [Fact]
    public void ParseResolvedVersion_finds_package_in_transitive_packages()
    {
        var json = """
            {
              "version": 1,
              "parameters": "--include-transitive",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "Duende.Storage.PostgreSql",
                          "requestedVersion": "1.2.0-preview.3",
                          "resolvedVersion": "1.2.0-preview.3"
                        }
                      ],
                      "transitivePackages": [
                        {
                          "id": "Duende.Storage",
                          "resolvedVersion": "1.2.0-preview.3"
                        },
                        {
                          "id": "Npgsql",
                          "resolvedVersion": "10.0.2"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBe("1.2.0-preview.3");
    }

    [Fact]
    public void ParseResolvedVersion_prefers_top_level_over_transitive()
    {
        var json = """
            {
              "version": 1,
              "parameters": "--include-transitive",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "Duende.Storage",
                          "requestedVersion": "1.2.0-preview.3",
                          "resolvedVersion": "1.2.0-preview.3"
                        }
                      ],
                      "transitivePackages": [
                        {
                          "id": "Duende.Storage",
                          "resolvedVersion": "1.1.0"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBe("1.2.0-preview.3");
    }

    [Fact]
    public void ParseResolvedVersion_returns_null_when_package_not_in_top_level_or_transitive()
    {
        var json = """
            {
              "version": 1,
              "parameters": "--include-transitive",
              "projects": [
                {
                  "path": "Test.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "SomeOtherPackage",
                          "requestedVersion": "1.0.0",
                          "resolvedVersion": "1.0.0"
                        }
                      ],
                      "transitivePackages": [
                        {
                          "id": "AnotherPackage",
                          "resolvedVersion": "2.0.0"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var result = ProjectContextScanner.ParseResolvedVersion(json, "Duende.Storage");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_unknown_plugin_name()
    {
        var result = await ProjectContextScanner.ResolveAsync("nonexistent", ".");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_returns_null_when_no_project_or_solution_exists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CliTests_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var result = await ProjectContextScanner.ResolveAsync("storage", tempDir);
            result.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveExplicit_uses_exact_version()
    {
        var result = ProjectContextScanner.ResolveExplicit("storage", "7.2.1");

        var resolved = result.ShouldNotBeNull();
        resolved.PackageId.ShouldBe("Duende.Storage.CliPlugin");
        resolved.Version.ShouldBe("[7.2.1]");
    }

    [Fact]
    public void ResolveExplicit_preserves_prerelease_version()
    {
        var result = ProjectContextScanner.ResolveExplicit("storage", "1.2.0-preview.1");

        var resolved = result.ShouldNotBeNull();
        resolved.PackageId.ShouldBe("Duende.Storage.CliPlugin");
        resolved.Version.ShouldBe("[1.2.0-preview.1]");
    }

    [Fact]
    public void ResolveExplicit_returns_null_for_unknown_plugin()
    {
        var result = ProjectContextScanner.ResolveExplicit("nonexistent", "1.0.0");

        result.ShouldBeNull();
    }
}
