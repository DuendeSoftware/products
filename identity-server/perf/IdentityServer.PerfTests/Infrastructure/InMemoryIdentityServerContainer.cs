// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.PerfTest.Infrastructure
{
    public class InMemoryIdentityServerContainer : IdentityServerContainer
    {
        public List<Client> Clients = new List<Client>();
        public List<IdentityResource> IdentityResources = new List<IdentityResource>();
        public List<ApiResource> ApiResources = new List<ApiResource>();
        public List<ApiScope> ApiScopes = new List<ApiScope>();

        //options.AddFilter("Duende.IdentityServer", LogLevel.Debug);

        public event Action<IdentityServerOptions> OnConfigureIdentityServerOptions = opts => { };

        public InMemoryIdentityServerContainer()
        {
            OnConfigureServices += services => 
            {
                services.AddIdentityServer(OnConfigureIdentityServerOptions)
                    .AddInMemoryClients(Clients)
                    .AddInMemoryIdentityResources(IdentityResources)
                    .AddInMemoryApiScopes(ApiScopes)
                    .AddInMemoryApiResources(ApiResources);
            };

            OnConfigure += app => 
            {
                app.UseIdentityServer();
            };
        }
    }
}

