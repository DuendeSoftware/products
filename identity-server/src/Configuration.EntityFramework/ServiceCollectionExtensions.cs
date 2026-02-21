// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.IdentityServer.Configuration.EntityFramework;


/// <summary>
/// Extension methods for adding the entity framework based client configuration
/// store implementation to DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the entity framework based client configuration store
    /// implementation to DI.
    /// </summary>
    public static IServiceCollection AddClientConfigurationStore(this IdentityServerConfigurationBuilder builder)
    {
        builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        return builder.Services.AddTransient<IClientConfigurationStore, ClientConfigurationStore>();
    }
}
