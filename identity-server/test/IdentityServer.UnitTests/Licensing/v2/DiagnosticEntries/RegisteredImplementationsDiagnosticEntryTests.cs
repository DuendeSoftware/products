// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class RegisteredImplementationsDiagnosticEntryTests
{
    [Fact]
    public async Task WriteAsync_ShouldWriteRegisteredImplementationInfo()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<IProfileService, MockProfileService>();
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(serviceCollection), new NullLogger<RegisteredImplementationsDiagnosticEntry>());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var namespaceEntry = registeredImplementations.GetProperty(typeof(IProfileService).Namespace!);
        var profileServiceEntry = namespaceEntry.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        var expectedTypeInfo = typeof(MockProfileService);
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe(expectedTypeInfo.FullName);
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Name);
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Version?.ToString());
    }

    [Fact]
    public async Task WriteAsync_GroupsImplementationsByNamespace()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<IProfileService, MockProfileService>()
            .AddSingleton<IClientStore, InMemoryClientStore>();
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(serviceCollection), new NullLogger<RegisteredImplementationsDiagnosticEntry>());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        registeredImplementations.TryGetProperty(typeof(IProfileService).Namespace!, out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty(typeof(IClientStore).Namespace!, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_HandlesNoServiceRegisteredForInterface()
    {
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollectionAccessor(new ServiceCollection()), new NullLogger<RegisteredImplementationsDiagnosticEntry>());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var namespaceEntry = registeredImplementations.GetProperty(typeof(IProfileService).Namespace!);
        var profileServiceEntry = namespaceEntry.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe("Not Registered");
    }
}
