// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130
namespace Duende.Storage.Internal.Builder;
#pragma warning restore IDE0130

public static class DsoRegistrationServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddDsoRegistration<TDso>() where TDso : IDataStorageObject
        {
            var dsoRegistration = new DsoRegistration(typeof(TDso), TDso.DsoVersion);
            var dsoRegistrationServiceDescriptor = new ServiceDescriptor(typeof(DsoRegistration), dsoRegistration);
            services.Add(dsoRegistrationServiceDescriptor);
        }
    }
}
