// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class RegisteredImplementationsDiagnosticEntryTests
{
    [Fact]
    public async Task WriteAsync_ShouldWriteRegisteredImplementationInfo()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<IProfileService, MockProfileService>();
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(serviceCollection));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var services = registeredImplementations.GetProperty("Services");
        var profileServiceEntry = services.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        var expectedTypeInfo = typeof(MockProfileService);
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe(expectedTypeInfo.FullName);
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Name);
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Version?.ToString());
    }

    [Fact]
    public async Task WriteAsync_GroupsImplementationsByCategory()
    {
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(new ServiceCollection()));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        registeredImplementations.TryGetProperty("Root", out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty("Hosting", out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty("Infrastructure", out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty("ResponseHandling", out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty("Services", out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty("Stores", out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty("Validation", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_HandlesMultipleRegistrationsForAService()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<IProfileService, DefaultProfileService>()
            .AddSingleton<IProfileService, MockProfileService>();
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(serviceCollection));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var services = registeredImplementations.GetProperty("Services");
        var profileServiceEntry = services.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var firstAssemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        var firstExpectedTypeInfo = typeof(DefaultProfileService);
        firstAssemblyInfo.GetProperty("TypeName").GetString().ShouldBe(firstExpectedTypeInfo.FullName);
        firstAssemblyInfo.GetProperty("Assembly").GetString().ShouldBe(firstExpectedTypeInfo.Assembly.GetName().Name);
        firstAssemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe(firstExpectedTypeInfo.Assembly.GetName().Version?.ToString());
        var secondAssemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().Last();
        var secondExpectedTypeInfo = typeof(MockProfileService);
        secondAssemblyInfo.GetProperty("TypeName").GetString().ShouldBe(secondExpectedTypeInfo.FullName);
        secondAssemblyInfo.GetProperty("Assembly").GetString().ShouldBe(secondExpectedTypeInfo.Assembly.GetName().Name);
        secondAssemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe(secondExpectedTypeInfo.Assembly.GetName().Version?.ToString());
    }

    [Fact]
    public async Task WriteAsync_HandlesNoServiceRegisteredForInterface()
    {
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(new ServiceCollection()));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var services = registeredImplementations.GetProperty("Services");
        var profileServiceEntry = services.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe("Not Registered");
    }

    [Fact]
    public async Task WriteAsync_ShouldIncludeAllPublicInterfaces()
    {
        var interfaces = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsInterface && type.IsPublic && type.Namespace != null &&
                           type.Namespace.StartsWith(
                               "Duende.IdentityServer"))
            .Select(type => type.Name);
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(new ServiceCollection()));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var entries = registeredImplementations.EnumerateObject()
            .SelectMany(property => property.Value.EnumerateArray()).Select(element => element.EnumerateObject().First().Name);
        entries.ShouldBe(interfaces, ignoreOrder: true);
    }
}
