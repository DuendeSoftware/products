// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Clients.Setup;

public class Startup
{
    static public ICustomTokenRequestValidator CustomTokenRequestValidator { get; set; }

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
        builder.AddTestUsers(Users.Get());

        builder.AddDeveloperSigningCredential(persistKey: false);

        builder.AddExtensionGrantValidator<ExtensionGrantValidator>();
        builder.AddExtensionGrantValidator<ExtensionGrantValidator2>();
        builder.AddExtensionGrantValidator<NoSubjectExtensionGrantValidator>();
        builder.AddExtensionGrantValidator<DynamicParameterExtensionGrantValidator>();

        builder.AddProfileService<CustomProfileService>();

        builder.AddJwtBearerClientAuthentication();
        builder.AddSecretValidator<ConfirmationSecretValidator>();

        // add a custom token request validator if set
        if (CustomTokenRequestValidator != null)
        {
            builder.Services.AddTransient(r => CustomTokenRequestValidator);
        }
    }

    public void Configure(IApplicationBuilder app) => app.UseIdentityServer();
}
