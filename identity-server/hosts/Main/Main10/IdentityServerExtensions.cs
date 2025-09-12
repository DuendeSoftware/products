// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosts.Shared.Configuration;
using Duende.IdentityServer.Hosts.Shared.Customization;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.IdentityModel.Tokens;

namespace IdentityServerHost;

#pragma warning disable IDE0058 // Expression value is never used. Disabled throughout the entire file, as all builder.Services or app.Use would need a discard

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

                options.Endpoints.EnablePushedAuthorizationEndpoint = true;
                options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;

                options.KeyManagement.SigningAlgorithms.Add(new SigningAlgorithmOptions
                {
                    Name = "RS256",
                    UseX509Certificate = true
                });

                // In load-balanced environments, synchronization delay is important.
                // In development, we're never load balanced and can skip it to start up faster.
                if (builder.Environment.IsDevelopment())
                {
                    options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.Zero;
                }

                options.MutualTls.Enabled = true;

                options.Diagnostics.ChunkSize = 1024 * 1000 - 32; // 1 MB minus some formatting space;
            })
            .AddServerSideSessions()
            .AddInMemoryClients([.. TestClients.Get()])
            .AddInMemoryIdentityResources(TestResources.IdentityResources)
            .AddInMemoryApiResources(TestResources.ApiResources)
            .AddInMemoryApiScopes(TestResources.ApiScopes)
            //.AddStaticSigningCredential()
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
            ])
            .AddLicenseSummary();

        builder.Services.AddIdentityServerConfiguration(opt => { })
            .AddInMemoryClientConfigurationStore();

        builder.Services.AddDistributedMemoryCache();

        builder.Services.AddAuthentication().AddCertificate(certificateOptions =>
        {
            // We must allow self-signed certificates for the "ephemeral" case
            certificateOptions.AllowedCertificateTypes = CertificateTypes.Chained | CertificateTypes.SelfSigned;
            certificateOptions.RevocationMode = X509RevocationMode.NoCheck;
        });

        return builder;
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
