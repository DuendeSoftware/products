// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

public static class ServiceCollectionDecorationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Replaces the most recent registration of <typeparamref name="TService"/> with a decorator
        /// produced by <paramref name="decorate"/>. The decorator receives the original (inner) service
        /// instance and the <see cref="IServiceProvider"/>. The original registration's lifetime is preserved.
        /// </summary>
        public IServiceCollection Decorate<TService>(Func<TService, IServiceProvider, TService> decorate)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(decorate);

            var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
                ?? throw new InvalidOperationException(
                    $"No registration found for {typeof(TService).Name} to decorate.");
            _ = services.Remove(descriptor);

            services.Add(new ServiceDescriptor(
                typeof(TService),
                sp =>
                {
                    var inner = (TService)(descriptor.ImplementationInstance
                        ?? descriptor.ImplementationFactory?.Invoke(sp)
                        ?? ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
                    return decorate(inner, sp);
                },
                descriptor.Lifetime));

            return services;
        }
    }
}
