// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class RegisteredImplementationsDiagnosticEntryTests
{
    [Fact]
    public async Task Should_Not_Write_NonDefault_Implementations()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<IProfileService, DefaultProfileService>();
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(serviceCollection));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        registeredImplementations.TryGetProperty("Services", out _).ShouldBeTrue();
        var services = registeredImplementations.GetProperty("Services");
        services.EnumerateArray().Any(entry => entry.TryGetProperty(nameof(IProfileService), out _)).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Write_Type_Information_For_Non_Default_Implementation()
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
    public async Task Should_Group_Registered_Implementations_By_Category()
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
    public async Task Should_Handle_Multiple_Registered_Non_Default_Implementations()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<IProfileService, TestProfileService>()
            .AddSingleton<IProfileService, MockProfileService>();
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(serviceCollection));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var services = registeredImplementations.GetProperty("Services");
        var profileServiceEntry = services.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var firstAssemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        var firstExpectedTypeInfo = typeof(TestProfileService);
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
    public async Task Should_Handle_No_Non_Default_Implementations_Registered()
    {
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(new ServiceCollection()));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var services = registeredImplementations.GetProperty("Services");
        services.EnumerateArray().Any(entry => entry.TryGetProperty(nameof(IProfileService), out _)).ShouldBeFalse();
    }

    [Fact]
    public void Should_Track_All_Public_Interfaces()
    {
        var interfaces = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsInterface && type.IsPublic && type.Namespace != null &&
                           type.Namespace.StartsWith(
                               "Duende.IdentityServer"))
            .Select(type => type);
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(new ServiceCollection()));
        var typesTrackedField = typeof(RegisteredImplementationsDiagnosticEntry)
            .GetField("_typesToInspect", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(subject) as Dictionary<string, IEnumerable<RegisteredImplementationDetails>>;
        var typesTracked = typesTrackedField?.SelectMany(kvp => kvp.Value).Select(details => details.TInterface);

        typesTracked.ShouldBe(interfaces, ignoreOrder: true);
    }
}
