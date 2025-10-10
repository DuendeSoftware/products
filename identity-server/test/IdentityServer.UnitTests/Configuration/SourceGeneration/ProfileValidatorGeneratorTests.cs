// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Duende.IdentityServer.UnitTests.Configuration.SourceGeneration;

/// <summary>
/// Tests for ProfileValidatorGenerator source generator.
/// </summary>
public class ProfileValidatorGeneratorTests
{
    private const string Category = "Source Generator";

    [Fact]
    [Trait("Category", Category)]
    public void simple_class_generates_validator()
    {
        var source = """
            using Duende.IdentityServer.Configuration.Profiles;

            namespace TestNamespace;

            [GenerateProfileValidator]
            public class TestOptions
            {
                public string StringProperty { get; set; } = default!;
                public int IntProperty { get; set; }
                public bool BoolProperty { get; set; }
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.Length.ShouldBe(1);

        var generatedSource = result.GeneratedSources[0].SourceText.ToString();

        // Verify class was generated
        generatedSource.ShouldContain("public sealed class TestOptionsProfileValidator");
        generatedSource.ShouldContain("namespace TestNamespace;");

        // Verify all three properties have methods
        generatedSource.ShouldContain("public ProfilePropertyValidator<string> StringProperty()");
        generatedSource.ShouldContain("public ProfilePropertyValidator<int> IntProperty()");
        generatedSource.ShouldContain("public ProfilePropertyValidator<bool> BoolProperty()");

        // Verify methods use correct property paths
        generatedSource.ShouldContain("\"StringProperty\"");
        generatedSource.ShouldContain("\"IntProperty\"");
        generatedSource.ShouldContain("\"BoolProperty\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public void nested_properties_generates_validator_methods()
    {
        var source = """
            using Duende.IdentityServer.Configuration.Profiles;

            namespace TestNamespace;

            [GenerateProfileValidator]
            public class TestOptions
            {
                public EndpointsOptions Endpoints { get; set; } = default!;
            }

            public class EndpointsOptions
            {
                public bool EnableDiscoveryEndpoint { get; set; }
                public bool EnableTokenEndpoint { get; set; }
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();

        var generatedSource = result.GeneratedSources[0].SourceText.ToString();

        // Verify root property
        generatedSource.ShouldContain("public ProfilePropertyValidator<TestNamespace.EndpointsOptions> Endpoints()");
        generatedSource.ShouldContain("\"Endpoints\"");

        // Verify nested properties with flattened method names
        generatedSource.ShouldContain("public ProfilePropertyValidator<bool> EndpointsEnableDiscoveryEndpoint()");
        generatedSource.ShouldContain("\"Endpoints.EnableDiscoveryEndpoint\"");

        generatedSource.ShouldContain("public ProfilePropertyValidator<bool> EndpointsEnableTokenEndpoint()");
        generatedSource.ShouldContain("\"Endpoints.EnableTokenEndpoint\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public void deeply_nested_properties_generates_validator_methods()
    {
        var source = """
            using Duende.IdentityServer.Configuration.Profiles;

            namespace TestNamespace;

            [GenerateProfileValidator]
            public class TestOptions
            {
                public Level1 First { get; set; } = default!;
            }

            public class Level1
            {
                public Level2 Second { get; set; } = default!;
            }

            public class Level2
            {
                public string DeepProperty { get; set; } = default!;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();

        var generatedSource = result.GeneratedSources[0].SourceText.ToString();

        // Verify deeply nested property
        generatedSource.ShouldContain("public ProfilePropertyValidator<string> FirstSecondDeepProperty()");
        generatedSource.ShouldContain("\"First.Second.DeepProperty\"");
        generatedSource.ShouldContain("_instance.First.Second.DeepProperty");
    }

    [Fact]
    [Trait("Category", Category)]
    public void readonly_property_not_included()
    {
        var source = """
            using Duende.IdentityServer.Configuration.Profiles;

            namespace TestNamespace;

            [GenerateProfileValidator]
            public class TestOptions
            {
                public string WritableProperty { get; set; } = default!;
                public string ReadOnlyProperty { get; } = "readonly";
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();

        var generatedSource = result.GeneratedSources[0].SourceText.ToString();

        // Verify writable property is included
        generatedSource.ShouldContain("public ProfilePropertyValidator<string> WritableProperty()");

        // Verify readonly property is NOT included
        generatedSource.ShouldNotContain("ReadOnlyProperty()");
    }

    [Fact]
    [Trait("Category", Category)]
    public void multiple_classes_generates_multiple_validators()
    {
        var source = """
            using Duende.IdentityServer.Configuration.Profiles;

            namespace TestNamespace;

            [GenerateProfileValidator]
            public class FirstOptions
            {
                public string Property1 { get; set; } = default!;
            }

            [GenerateProfileValidator]
            public class SecondOptions
            {
                public int Property2 { get; set; }
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.Length.ShouldBe(2);

        var allGenerated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));

        // Verify both validators were generated
        allGenerated.ShouldContain("public sealed class FirstOptionsProfileValidator");
        allGenerated.ShouldContain("public sealed class SecondOptionsProfileValidator");
        allGenerated.ShouldContain("public ProfilePropertyValidator<string> Property1()");
        allGenerated.ShouldContain("public ProfilePropertyValidator<int> Property2()");
    }

    [Fact]
    [Trait("Category", Category)]
    public void complex_types_skips_collections()
    {
        var source = """
            using System.Collections.Generic;
            using Duende.IdentityServer.Configuration.Profiles;

            namespace TestNamespace;

            [GenerateProfileValidator]
            public class TestOptions
            {
                public string SimpleProperty { get; set; } = default!;
                public List<string> CollectionProperty { get; set; } = default!;
                public Dictionary<string, string> DictionaryProperty { get; set; } = default!;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();

        var generatedSource = result.GeneratedSources[0].SourceText.ToString();

        // Verify simple property is included
        generatedSource.ShouldContain("public ProfilePropertyValidator<string> SimpleProperty()");

        // Verify collections are included at top level but not walked into
        generatedSource.ShouldContain("CollectionProperty()");
        generatedSource.ShouldContain("DictionaryProperty()");

        // Collections should not be walked for nested properties
        generatedSource.ShouldNotContain("CollectionPropertyCount"); // Should not walk List<T> members
    }

    [Fact]
    [Trait("Category", Category)]
    public void no_attribute_no_validator_generated()
    {
        var source = """
            namespace TestNamespace;

            public class TestOptions
            {
                public string Property { get; set; } = default!;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void nullable_reference_types_preserved_in_generics()
    {
        var source = """
            using System.Threading.Tasks;
            using Duende.IdentityServer.Configuration.Profiles;

            namespace TestNamespace;

            [GenerateProfileValidator]
            public class TestOptions
            {
                public Task<string?> NullableTaskProperty { get; set; } = default!;
                public Task<string> NonNullableTaskProperty { get; set; } = default!;
            }
            """;

        var result = RunGenerator(source);

        result.Diagnostics.ShouldBeEmpty();

        var generatedSource = result.GeneratedSources[0].SourceText.ToString();

        // Verify nullable is preserved
        generatedSource.ShouldContain("public ProfilePropertyValidator<System.Threading.Tasks.Task<string?>> NullableTaskProperty()");
        generatedSource.ShouldContain("public ProfilePropertyValidator<System.Threading.Tasks.Task<string>> NonNullableTaskProperty()");
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        // Create a syntax tree from the source
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Create references for the compilation
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        // Add System.Collections references for collection tests
        var systemCollectionsAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Collections");
        if (systemCollectionsAssembly != null)
        {
            references.Add(MetadataReference.CreateFromFile(systemCollectionsAssembly.Location));
        }

        // Add System.Runtime for Task<T>
        var systemRuntimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (systemRuntimeAssembly != null)
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimeAssembly.Location));
        }

        // Create a compilation
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Add the attribute source
        var attributeSource = """
            namespace Duende.IdentityServer.Configuration.Profiles
            {
                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
                public sealed class GenerateProfileValidatorAttribute : System.Attribute
                {
                }
            }
            """;
        var attributeSyntaxTree = CSharpSyntaxTree.ParseText(attributeSource);
        compilation = compilation.AddSyntaxTrees(attributeSyntaxTree);

        // Create the generator
        var generator = new SourceGenerators.ProfileValidatorGenerator();

        // Run the generator
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        return runResult.Results[0];
    }
}

