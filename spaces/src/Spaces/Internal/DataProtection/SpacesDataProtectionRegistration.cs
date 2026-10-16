// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Telemetry;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.Spaces.Internal.DataProtection;

internal static class SpacesDataProtectionRegistration
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddDataProtection();
        _ = services.AddMetrics();
        services.TryAddSingleton<SpacesMetrics>();

        // The effective registration is the last one: it is what a single resolve returns.
        // Select by index from the end, because the same descriptor instance may appear more than once.
        var index = -1;
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IDataProtectionProvider) && !services[i].IsKeyedService)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        var original = services[index];
        if (original.Lifetime == ServiceLifetime.Scoped)
        {
            throw new InvalidOperationException(
                "IDataProtectionProvider is registered as scoped, which Spaces data protection does not support. " +
                "Register it as a singleton before AddSpaces().");
        }

        // The original moves to a private key, so the container keeps building and disposing it exactly as
        // the app registered it. The partitioned provider is a singleton that resolves it once; a transient
        // original is therefore resolved from the root provider and must not depend on scoped services.
        // With partitioning off the original itself is returned, so the container also tracks it as this
        // factory's result and may dispose it twice (Dispose must be idempotent). An instance registration
        // is then disposed by the container even though the app owns it.
        var key = new object();
        var installed = ServiceDescriptor.Singleton<IDataProtectionProvider>(sp =>
        {
            var inner = sp.GetRequiredKeyedService<IDataProtectionProvider>(key);
            var options = sp.GetRequiredService<IOptions<SpacesOptions>>().Value;
            if (!options.PartitionDataProtection)
            {
                return inner;
            }

            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Duende.Spaces.DataProtection")
                ?? NullLogger.Instance;
            return new SpacePartitionedDataProtectionProvider(
                inner,
                sp.GetRequiredService<ISpaceContextAccessor>(),
                sp.GetRequiredService<SpacesMetrics>(),
                logger);
        });

        services[index] = installed;
        services.Add(ToKeyed(original, key));
        _ = services.AddSingleton(new SpacesDataProtectionRegistrationState(services, installed));
    }

    private static ServiceDescriptor ToKeyed(ServiceDescriptor descriptor, object key)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            return new ServiceDescriptor(descriptor.ServiceType, key, instance);
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return new ServiceDescriptor(descriptor.ServiceType, key, (sp, _) => factory(sp), descriptor.Lifetime);
        }

        return new ServiceDescriptor(descriptor.ServiceType, key, descriptor.ImplementationType!, descriptor.Lifetime);
    }
}
