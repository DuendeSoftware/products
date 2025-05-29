// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Models;
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
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IProfileService, MockProfileService>()
            .BuildServiceProvider();
        var subject = new RegisteredImplementationsDiagnosticEntry(serviceProvider, new NullLogger<RegisteredImplementationsDiagnosticEntry>());

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
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IProfileService, MockProfileService>()
            .AddSingleton<IClientStore, InMemoryClientStore>()
            .BuildServiceProvider();
        var subject = new RegisteredImplementationsDiagnosticEntry(serviceProvider, new NullLogger<RegisteredImplementationsDiagnosticEntry>());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        registeredImplementations.TryGetProperty(typeof(IProfileService).Namespace!, out _).ShouldBeTrue();
        registeredImplementations.TryGetProperty(typeof(IClientStore).Namespace!, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_HandlesNoServiceRegisteredForInterface()
    {
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollection().BuildServiceProvider(), new NullLogger<RegisteredImplementationsDiagnosticEntry>());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var namespaceEntry = registeredImplementations.GetProperty(typeof(IProfileService).Namespace!);
        var profileServiceEntry = namespaceEntry.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe("Not Registered");
    }

    [Fact]
    public async Task WriteAsync_HandlesConstructorThatThrows()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IProfileService, ThrowingConstructorProfileService>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var subject = new RegisteredImplementationsDiagnosticEntry(serviceProvider, new NullLogger<RegisteredImplementationsDiagnosticEntry>());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var namespaceEntry = registeredImplementations.GetProperty(typeof(IProfileService).Namespace!);
        var profileServiceEntry = namespaceEntry.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe("Error resolving service");
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe("Error resolving service");
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe("Error resolving service");
    }

    //NOTE: This test is here as a reminder of potential performance issues resolving implementations which
    //have slow constructors rather than testing any specific behavior.
    [Fact]
    public async Task WriteAsync_HandlesSlowConstructor()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IProfileService, SlowConstructorProfileService>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var subject = new RegisteredImplementationsDiagnosticEntry(serviceProvider, new NullLogger<RegisteredImplementationsDiagnosticEntry>());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredImplementations = result.RootElement.GetProperty("RegisteredImplementations");
        var namespaceEntry = registeredImplementations.GetProperty(typeof(IProfileService).Namespace!);
        var profileServiceEntry = namespaceEntry.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = profileServiceEntry.GetProperty(nameof(IProfileService)).EnumerateArray().First();
        var expectedTypeInfo = typeof(SlowConstructorProfileService);
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe(expectedTypeInfo.FullName);
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Name);
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Version?.ToString());
    }

    private class ThrowingConstructorProfileService : IProfileService
    {
        public ThrowingConstructorProfileService() => throw new Exception("Test exception in constructor");

        public Task GetProfileDataAsync(ProfileDataRequestContext context) => throw new NotImplementedException();

        public Task IsActiveAsync(IsActiveContext context) => throw new NotImplementedException();
    }

    private class SlowConstructorProfileService : IProfileService
    {
        public SlowConstructorProfileService() => Thread.Sleep(10);

        public Task GetProfileDataAsync(ProfileDataRequestContext context) => throw new NotImplementedException();

        public Task IsActiveAsync(IsActiveContext context) => throw new NotImplementedException();
    }
}
