// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using IdentityServerHost.Configuration;
using IdentityServerHost.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace IdentityServerHost;

internal static class IdentityServerExtensions
{
    internal static void ConfigureIdentityServer(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityServer(options =>
            {
                options.Events.RaiseSuccessEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;

                options.EmitScopesAsSpaceDelimitedStringInJwt = true;
                options.Endpoints.EnableJwtRequestUri = true;

                options.ServerSideSessions.UserDisplayNameClaimType = JwtClaimTypes.Name;

                options.UserInteraction.CreateAccountUrl = "/Account/Create";

                options.Endpoints.EnablePushedAuthorizationEndpoint = true;
                options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;

                options.KeyManagement.SigningAlgorithms.Add(new SigningAlgorithmOptions
                {
                    Name = "RS256",
                    UseX509Certificate = true
                });
            })
            .AddServerSideSessions()
            .AddInMemoryClients(Clients.Get().ToList())
            .AddInMemoryIdentityResources(Resources.IdentityResources)
            .AddInMemoryApiScopes(Resources.ApiScopes)
            .AddInMemoryApiResources(Resources.ApiResources)
            //.AddStaticSigningCredential()
            .AddExtensionGrantValidator<Extensions.ExtensionGrantValidator>()
            .AddExtensionGrantValidator<Extensions.NoSubjectExtensionGrantValidator>()
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
            ])
            .AddLicenseSummary();


        builder.Services.AddDistributedMemoryCache();

        // builder.Services.AddIdentityServerConfiguration(opt =>
        // {
        //     // opt.DynamicClientRegistration.SecretLifetime = TimeSpan.FromHours(1);
        // }).AddInMemoryClientConfigurationStore();
    }

    // To use static signing credentials, create keys and add it to the certificate store.
    // This shows how to create both rsa and ec keys, in case you had clients that were configured to use different algorithms
    // You can create keys for dev use with the mkcert util:
    //    mkcert -pkcs12 identityserver.test.rsa
    //    mkcert -pkcs12 -ecdsa identityserver.test.ecdsa
    // Then import the keys into the certificate manager. This code expect keys in the personal store of the current user.
    private static IIdentityServerBuilder AddStaticSigningCredential(this IIdentityServerBuilder builder)
    {
        var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        try
        {
            store.Open(OpenFlags.ReadOnly);

            var rsaCert = store.Certificates
                .Find(X509FindType.FindBySubjectName, "identityserver.test.rsa", true)
                .Single();
            builder.AddSigningCredential(rsaCert, "RS256");
            builder.AddSigningCredential(rsaCert, "PS256");

            var ecCert = store.Certificates
                .Find(X509FindType.FindBySubjectName, "identityserver.test.ecdsa", true)
                .Single();
            var key = new ECDsaSecurityKey(ecCert.GetECDsaPrivateKey())
            {
                KeyId = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)
            };
            builder.AddSigningCredential(key, IdentityServerConstants.ECDsaSigningAlgorithm.ES256);
        }
        finally
        {
            store.Close();
        }

        return builder;
    }
}
