// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.EntityFramework;
using Duende.IdentityServer.Configuration.RequestProcessing;
using IdentityServerHost.Configuration;
using IdentityServerHost.Extensions;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IdentityServerHost;

internal static class IdentityServerExtensions
{
    internal static WebApplicationBuilder ConfigureIdentityServer(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        builder.Services.AddIdentityServer(options =>
        {
            options.Authentication.CoordinateClientLifetimesWithUserSession = true;
            options.ServerSideSessions.UserDisplayNameClaimType = JwtClaimTypes.Name;
            options.ServerSideSessions.RemoveExpiredSessions = true;
            options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;
            options.Endpoints.EnablePushedAuthorizationEndpoint = true;

            // Imported options
            options.Events.RaiseSuccessEvents = true;
            options.Events.RaiseFailureEvents = true;
            options.Events.RaiseErrorEvents = true;
            options.Events.RaiseInformationEvents = true;

            options.EmitScopesAsSpaceDelimitedStringInJwt = true;
            options.Endpoints.EnableJwtRequestUri = true;

            options.UserInteraction.CreateAccountUrl = "/Account/Create";

            options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;

            options.KeyManagement.SigningAlgorithms.Add(new SigningAlgorithmOptions
            {
                Name = "RS256",
                UseX509Certificate = true
            });

            options.MutualTls.Enabled = true;

            options.Diagnostics.ChunkSize = 1024 * 1000 - 32; // 1 MB minus some formatting space;
        })
            .AddTestUsers(TestUsers.Users)
            // this adds the config data from DB (clients, resources, CORS)
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString);
            })
            // this adds the operational data from DB (codes, tokens, consents)
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString);

                // this enables automatic token cleanup. this is optional.
                options.EnableTokenCleanup = true;
                options.RemoveConsumedTokens = true;
            })
            .AddAppAuthRedirectUriValidator()
            .AddServerSideSessions()

            // this is something you will want in production to reduce load on and requests to the DB
            //.AddConfigurationStoreCache()

            //.AddStaticSigningCredential()
            .AddExtensionGrantValidator<ExtensionGrantValidator>()
            .AddExtensionGrantValidator<NoSubjectExtensionGrantValidator>()
            .AddJwtBearerClientAuthentication()
            .AddAppAuthRedirectUriValidator()
            .AddProfileService<HostProfileService>()
            .AddCustomTokenRequestValidator<ParameterizedScopeTokenRequestValidator>()
            .AddScopeParser<ParameterizedScopeParser>()
            .AddMutualTlsSecretValidators()
            .AddLicenseSummary();

        builder.Services.AddDistributedMemoryCache();

        builder.Services.AddIdentityServerConfiguration(opt =>
        {
            // opt.DynamicClientRegistration.SecretLifetime = TimeSpan.FromHours(1);
        })
            .AddClientConfigurationStore();

        builder.Services.AddTransient<IDynamicClientRegistrationRequestProcessor, CustomClientRegistrationProcessor>();

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
    // Then import the keys into the certificate manager. This code expects keys in the personal store of the current user.
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
