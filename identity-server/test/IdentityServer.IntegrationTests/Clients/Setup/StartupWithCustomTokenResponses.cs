// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Clients.Setup;

public class StartupWithCustomTokenResponses
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthentication();

        var builder = services.AddIdentityServer(options =>
        {
            options.IssuerUri = "https://idsvr4";

            options.Events = new EventsOptions
            {
                RaiseErrorEvents = true,
                RaiseFailureEvents = true,
                RaiseInformationEvents = true,
                RaiseSuccessEvents = true
            };
            options.KeyManagement.Enabled = false;
        });

        builder.AddInMemoryClients(Clients.Get());
        builder.AddInMemoryIdentityResources(Scopes.GetIdentityScopes());
        builder.AddInMemoryApiResources(Scopes.GetApiResources());
        builder.AddInMemoryApiScopes(Scopes.GetApiScopes());

        builder.AddDeveloperSigningCredential(persistKey: false);

        services.AddTransient<IResourceOwnerPasswordValidator, CustomResponseResourceOwnerValidator>();
        builder.AddExtensionGrantValidator<CustomResponseExtensionGrantValidator>();
    }

    public void Configure(IApplicationBuilder app) => app.UseIdentityServer();
}
