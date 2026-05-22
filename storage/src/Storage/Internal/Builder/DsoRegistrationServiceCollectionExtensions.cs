// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130
namespace Duende.Storage.Internal.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for registering DSO types in the service collection.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class DsoRegistrationServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a DSO type for deserialization support.
        /// </summary>
        /// <typeparam name="TDso">The DSO type to register.</typeparam>
        public void AddDsoRegistration<TDso>() where TDso : IDataStorageObject
        {
            var dsoRegistration = new DsoRegistration(typeof(TDso), TDso.DsoVersion);
            var dsoRegistrationServiceDescriptor = new ServiceDescriptor(typeof(DsoRegistration), dsoRegistration);
            services.Add(dsoRegistrationServiceDescriptor);
        }
    }
}
