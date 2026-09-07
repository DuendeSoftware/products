// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.IdentityServer;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void TryAddTransientOrDefault<TService, TDefault, TReplacement>()
            where TService : class
            where TDefault : class, TService
            where TReplacement : class, TService
        {
            var descriptor = services.LastOrDefault(x => x.ServiceType == typeof(TService));
            if (descriptor is null)
            {
                // Nothing registered yet, safe to add.
                services.AddTransient<TService, TReplacement>();
                return;
            }

            var actualType = descriptor.ImplementationType
                             ?? descriptor.ImplementationInstance?.GetType();

            // If we can't determine the type (factory registration), or it isn't
            // the default, assume the user customized it, leave alone.
            if (actualType != typeof(TDefault))
            {
                return;
            }

            services.RemoveAll<TService>();
            services.AddTransient<TService, TReplacement>();
        }

        public void TryAddSingletonOrDefault<TService, TDefault, TReplacement>()
            where TService : class
            where TDefault : class, TService
            where TReplacement : class, TService
        {
            var descriptor = services.LastOrDefault(x => x.ServiceType == typeof(TService));
            if (descriptor is null)
            {
                // Nothing registered yet, safe to add.
                services.AddSingleton<TService, TReplacement>();
                return;
            }

            var actualType = descriptor.ImplementationType
                             ?? descriptor.ImplementationInstance?.GetType();

            // If we can't determine the type (factory registration), or it isn't
            // the default, assume the user customized it, leave alone.
            if (actualType != typeof(TDefault))
            {
                return;
            }

            services.RemoveAll<TService>();
            services.AddSingleton<TService, TReplacement>();
        }
    }
}
