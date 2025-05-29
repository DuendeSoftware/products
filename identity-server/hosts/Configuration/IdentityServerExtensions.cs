// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.RequestProcessing;
using IdentityServerHost.Configuration;
using IdentityServerHost.Extensions;
using Microsoft.AspNetCore.Authentication.Certificate;

namespace IdentityServerHost;

internal static class IdentityServerExtensions
{
    internal static WebApplicationBuilder ConfigureIdentityServer(this WebApplicationBuilder builder)
    {
        var identityServer = builder.Services.AddIdentityServer(options =>
            {
                options.Events.RaiseSuccessEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;

                options.EmitScopesAsSpaceDelimitedStringInJwt = true;
                options.Endpoints.EnableJwtRequestUri = true;

                options.ServerSideSessions.UserDisplayNameClaimType = JwtClaimTypes.Name;

                options.UserInteraction.CreateAccountUrl = "/Account/Create";

                options.MutualTls.Enabled = true;
            })
            //.AddServerSideSessions()
            .AddInMemoryClients([])
            .AddInMemoryIdentityResources(TestResources.IdentityResources)
            .AddInMemoryApiScopes(TestResources.ApiScopes)
            .AddInMemoryApiResources(TestResources.ApiResources)
            .AddExtensionGrantValidator<ExtensionGrantValidator>()
            .AddExtensionGrantValidator<NoSubjectExtensionGrantValidator>()
            .AddJwtBearerClientAuthentication()
            .AddAppAuthRedirectUriValidator()
            .AddTestUsers(TestUsers.Users)
            .AddProfileService<HostProfileService>()
            .AddCustomTokenRequestValidator<ParameterizedScopeTokenRequestValidator>()
            .AddScopeParser<ParameterizedScopeParser>()
            .AddMutualTlsSecretValidators()
            .AddInMemoryOidcProviders(
            [
                new Duende.IdentityServer.Models.OidcProvider
                {
                    Scheme = "dynamicprovider-idsvr",
                    DisplayName = "IdentityServer (via Dynamic Providers)",
                    Authority = "https://demo.duendesoftware.com",
                    ClientId = "login",
                    ResponseType = "id_token",
                    Scope = "openid profile"
                }
            ]);

        builder.Services.AddAuthentication().AddCertificate(certificateOptions =>
        {
            // We must allow self-signed certificates for the "ephemeral" case
            certificateOptions.AllowedCertificateTypes = CertificateTypes.Chained | CertificateTypes.SelfSigned;
            certificateOptions.RevocationMode = X509RevocationMode.NoCheck;
        });

        builder.Services.AddIdentityServerConfiguration(opt =>
        {
            // opt.DynamicClientRegistration.SecretLifetime = TimeSpan.FromHours(1);
        }).AddInMemoryClientConfigurationStore();

        builder.Services.AddTransient<IDynamicClientRegistrationRequestProcessor, CustomClientRegistrationProcessor>();

        return builder;
    }
}
