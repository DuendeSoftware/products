// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Spaces.Internal.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Spaces.DataProtection;

public class SpacesDataProtectionValidationTests
{
    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        TestSpacesLicense.RegisterEntitled(services);
        return services;
    }

    private static ServiceCollection NewServicesWithSpaces()
    {
        var services = NewServices();
        services.AddSpacesInternal();
        return services;
    }

    private static void UseSpaceResolutionOn(ServiceCollection services)
    {
        using var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);
        app.UseSpaceResolution();
    }

    [Fact]
    public void valid_configuration_does_not_throw() =>
        Should.NotThrow(() => UseSpaceResolutionOn(NewServicesWithSpaces()));

    [Fact]
    public void same_descriptor_instance_registered_twice_with_partitioning_on_throws_for_another_provider()
    {
        var descriptor = ServiceDescriptor.Singleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        var services = NewServices();
        ((IServiceCollection)services).Add(descriptor);
        ((IServiceCollection)services).Add(descriptor);
        services.AddSpacesInternal();

        Should.Throw<InvalidOperationException>(() => UseSpaceResolutionOn(services))
            .Message.ShouldContain("another IDataProtectionProvider is registered");
    }

    [Fact]
    public void missing_registration_state_with_partitioning_on_throws()
    {
        var services = NewServices();
        services.AddOptions<SpacesOptions>();
        using var sp = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => SpacesDataProtectionValidation.Validate(sp))
            .Message.ShouldBe(
                "Spaces data protection was not registered. " +
                "Call AddSpaces() before building the app, or set SpacesOptions.PartitionDataProtection = false.");
    }

    [Fact]
    public void missing_registration_state_with_partitioning_off_does_not_throw()
    {
        var services = NewServices();
        services.AddOptions<SpacesOptions>().Configure(o => o.PartitionDataProtection = false);
        using var sp = services.BuildServiceProvider();

        Should.NotThrow(() => SpacesDataProtectionValidation.Validate(sp));
    }

    [Fact]
    public void ephemeral_provider_registered_after_add_spaces_throws()
    {
        var services = NewServicesWithSpaces();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        var ex = Should.Throw<InvalidOperationException>(() => UseSpaceResolutionOn(services));
        ex.Message.ShouldContain("registered or replaced after AddSpaces()");
    }

    [Fact]
    public void provider_replaced_after_add_spaces_throws()
    {
        var services = NewServicesWithSpaces();
        services.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));

        Should.Throw<InvalidOperationException>(() => UseSpaceResolutionOn(services))
            .Message.ShouldContain("PartitionDataProtection");
    }

    [Fact]
    public void provider_added_as_singleton_after_add_spaces_throws()
    {
        var services = NewServicesWithSpaces();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

        Should.Throw<InvalidOperationException>(() => UseSpaceResolutionOn(services))
            .Message.ShouldContain("PartitionDataProtection");
    }

    [Fact]
    public void multiple_non_keyed_providers_with_partitioning_on_throw()
    {
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSpacesInternal();

        var ex = Should.Throw<InvalidOperationException>(() => UseSpaceResolutionOn(services));
        ex.Message.ShouldBe(
            "SpacesOptions.PartitionDataProtection is enabled, but another IDataProtectionProvider is registered. " +
            "Register the provider once, before AddSpaces(), or set SpacesOptions.PartitionDataProtection = false.");
    }

    [Fact]
    public void multiple_non_keyed_providers_with_partitioning_off_do_not_throw()
    {
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSpacesInternal();
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = false);

        Should.NotThrow(() => UseSpaceResolutionOn(services));
    }

    [Fact]
    public void replaced_provider_with_partitioning_off_does_not_throw()
    {
        var services = NewServicesWithSpaces();
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = false);
        services.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));

        Should.NotThrow(() => UseSpaceResolutionOn(services));
    }

    [Fact]
    public void other_providers_are_detected_without_being_instantiated()
    {
        var factoryRuns = 0;
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(_ =>
        {
            factoryRuns++;
            return new EphemeralDataProtectionProvider();
        });
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSpacesInternal();

        Should.Throw<InvalidOperationException>(() => UseSpaceResolutionOn(services))
            .Message.ShouldContain("another IDataProtectionProvider is registered");
        factoryRuns.ShouldBe(0);
    }
}
