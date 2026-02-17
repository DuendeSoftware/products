// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.RequestProcessing;
using Duende.IdentityServer.Hosts.Shared.Configuration;
using Duende.IdentityServer.Hosts.Shared.Customization;
using Duende.IdentityServer.UI.AspNetIdentity.Models;

namespace IdentityServerHost;

internal static class IdentityServerExtensions
{
    internal static WebApplicationBuilder ConfigureIdentityServer(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityServer(opt =>
            {
                // In load-balanced environments, synchronization delay is important.
                // In development, we're never load balanced and can skip it to start up faster.
                if (builder.Environment.IsDevelopment())
                {
                    opt.KeyManagement.InitializationSynchronizationDelay = TimeSpan.Zero;
                }
            }
        )
            .AddInMemoryIdentityResources(TestResources.IdentityResources)
            .AddInMemoryApiResources(TestResources.ApiResources)
            .AddInMemoryApiScopes(TestResources.ApiScopes)
            .AddInMemoryClients(TestClients.Get())
            .AddAspNetIdentity<ApplicationUser>();

        builder.Services.AddIdentityServerConfiguration(opt => { })
            .AddInMemoryClientConfigurationStore();
        builder.Services.AddTransient<IDynamicClientRegistrationRequestProcessor, CustomClientRegistrationProcessor>();

        return builder;
    }
}
