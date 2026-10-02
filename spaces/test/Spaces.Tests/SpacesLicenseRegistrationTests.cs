// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Licensing;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public class SpacesLicenseRegistrationTests
{
    [Fact]
    public void license_registered_before_add_spaces_is_resolved_and_entitled()
    {
        var services = BuildServiceCollection();
        var registered = TestSpacesLicense.RegisterEntitledAndReturn(services);
        services.AddSpaces();

        using var serviceProvider = services.BuildServiceProvider();

        var license = TestSpacesLicense.ResolveV2License(serviceProvider);
        license.ShouldBeSameAs(registered);

        var validator = serviceProvider.GetRequiredService<SpacesLicenseValidator>();
        validator.ShouldNotBeNull();
        validator.ValidateSpaces().ShouldBeTrue();

        // AddSpaces() must supply TimeProvider itself; resolving the validator above already
        // proves this (LicenseValidator requires TimeProvider), but assert it explicitly too.
        serviceProvider.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void license_registered_after_add_spaces_is_resolved_and_entitled()
    {
        var services = BuildServiceCollection();
        services.AddSpaces();
        var registered = TestSpacesLicense.RegisterEntitledAndReturn(services);

        using var serviceProvider = services.BuildServiceProvider();

        var license = TestSpacesLicense.ResolveV2License(serviceProvider);
        license.ShouldBeSameAs(registered);

        var validator = serviceProvider.GetRequiredService<SpacesLicenseValidator>();
        validator.ShouldNotBeNull();
        validator.ValidateSpaces().ShouldBeTrue();
    }

    [Fact]
    public void missing_license_fails_at_resolution_for_boundary_services()
    {
        var services = BuildServiceCollection();
        services.AddSpaces();
        // Storage plumbing is needed so that resolving IPartitionedStorageFactory fails on the missing
        // V2License, rather than on an unrelated missing storage provider dependency.
        services.AddStorageInternal(b => b.AddSqliteInMemory());

        using var serviceProvider = services.BuildServiceProvider();

        AssertThrowsReferencingV2License(() => serviceProvider.GetRequiredService<ISpaceContextAccessor>());
        AssertThrowsReferencingV2License(() => serviceProvider.GetRequiredService<ISpaceAdmin>());
        AssertThrowsReferencingV2License(() => serviceProvider.GetRequiredService<IPartitionedStorageFactory>());
    }

    [Fact]
    public void missing_license_fails_when_resolving_license_validator_directly()
    {
        var services = BuildServiceCollection();
        services.AddSpaces();

        using var serviceProvider = services.BuildServiceProvider();

        AssertThrowsReferencingV2License(() => serviceProvider.GetRequiredService<SpacesLicenseValidator>());
    }

    private static ServiceCollection BuildServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }

    private static void AssertThrowsReferencingV2License(Action act)
    {
        var exception = Should.Throw<InvalidOperationException>(act);

        var messages = new List<string>();
        for (Exception? ex = exception; ex is not null; ex = ex.InnerException)
        {
            messages.Add(ex.Message);
        }

        messages.ShouldContain(m => m.Contains("V2License"), string.Join(" | ", messages));
    }
}
