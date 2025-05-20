// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class RegisteredImplementationsDiagnosticEntryTests
{
    [Fact]
    public async Task WriteAsync_ShouldWriteRegisteredServicesInfo()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IProfileService, MockProfileService>()
            .BuildServiceProvider();
        var subject = new RegisteredImplementationsDiagnosticEntry(serviceProvider);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredServices = result.RootElement.GetProperty("RegisteredServices");
        var services = registeredServices.GetProperty("Services");
        var firstEntry = services.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = firstEntry.GetProperty(nameof(IProfileService));
        var expectedTypeInfo = typeof(MockProfileService);
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe(expectedTypeInfo.FullName);
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Name);
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe(expectedTypeInfo.Assembly.GetName().Version?.ToString());
    }

    [Fact]
    public async Task WriteAsync_HandlesNoServiceRegisteredForInterface()
    {
        var subject = new RegisteredImplementationsDiagnosticEntry(new ServiceCollection().BuildServiceProvider());

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var registeredServices = result.RootElement.GetProperty("RegisteredServices");
        var services = registeredServices.GetProperty("Services");
        var firstEntry = services.EnumerateArray().ToList().SingleOrDefault(entry => entry.TryGetProperty(nameof(IProfileService), out _));
        var assemblyInfo = firstEntry.GetProperty(nameof(IProfileService));
        assemblyInfo.GetProperty("TypeName").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("Assembly").GetString().ShouldBe("Not Registered");
        assemblyInfo.GetProperty("AssemblyVersion").GetString().ShouldBe("Not Registered");
    }
}
