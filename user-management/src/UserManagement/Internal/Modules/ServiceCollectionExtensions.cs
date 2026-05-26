// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Internal.Modules;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void RegisterModule<TModule>()
            where TModule : IDuendeModule
        {
            var existing = services.FirstOrDefault(x => x.ServiceType == typeof(IDuendeModule) && x.ImplementationType == typeof(TModule));

            // Only register once
            if (existing is not null)
            {
                return;
            }

            // We can't register it using generic AddSingleton<> because of the
            // static abstract 'Register' method. But this does exaclty the same
            _ = services.AddSingleton(typeof(IDuendeModule), typeof(TModule));
            _ = services.AddSingleton(typeof(TModule));

            TModule.Register(services);
        }

        public void RegisterFeature<TFeature>() where TFeature : class, IDuendePlatformFeature => services.AddSingleton<IDuendePlatformFeature, TFeature>();

        public void RegisterDsoType<TDso>() where TDso : IDataStorageObject => services.AddDsoRegistration<TDso>();
    }

}
