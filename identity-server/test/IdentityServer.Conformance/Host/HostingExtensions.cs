// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.LocalApiAuthentication;
using Duende.IdentityServer.Hosts.Shared.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.UI;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Conformance.Host;

internal static class HostingExtensions
{
    internal static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddHealthChecks();

        builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseSuccessEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;

                options.EmitScopesAsSpaceDelimitedStringInJwt = true;

                // Reduce PAR lifetime to 65 seconds so the par-attempt-to-use-expired-request_uri
                // test can complete within the module timeout. The default is 10 minutes.
                options.PushedAuthorization.Lifetime = 65;

                // FAPI2 requires PS256 for id_token signing.
                options.KeyManagement.SigningAlgorithms =
                [
                    new SigningAlgorithmOptions(SecurityAlgorithms.RsaSsaPssSha256),
                ];

                // FAPI2 requires PS256 for client assertions; restrict to PS256 only.
                options.SupportedClientAssertionSigningAlgorithms =
                [
                    SecurityAlgorithms.RsaSsaPssSha256,
                ];

                // FAPI2 requires that client assertion nbf not be more than 60 seconds in the future.
                // Reduce clock skew from the default 5 minutes to 60 seconds.
                options.JwtValidationClockSkew = TimeSpan.FromSeconds(60);

                // FAPI2 (FAPI2-SP-ID2-5.4) restricts DPoP proof signing to PS/ES algorithms only.
                options.DPoP.SupportedDPoPSigningAlgorithms =
                [
                    SecurityAlgorithms.RsaSsaPssSha256,
                    SecurityAlgorithms.RsaSsaPssSha384,
                    SecurityAlgorithms.RsaSsaPssSha512,
                    SecurityAlgorithms.EcdsaSha256,
                    SecurityAlgorithms.EcdsaSha384,
                    SecurityAlgorithms.EcdsaSha512,
                ];

                // Advertise the DCR endpoint in discovery so all plans can find it.
                options.Discovery.DynamicClientRegistration.RegistrationEndpointMode =
                    RegistrationEndpointMode.Inferred;

                var issuerUri = builder.Configuration["IdentityServer:IssuerUri"];
                if (!string.IsNullOrEmpty(issuerUri))
                {
                    options.IssuerUri = issuerUri;
                }
            })
            // Static clients for the OidcCore plan (static_client variant).
            // Logout and FAPI 2.0 plans register clients dynamically via DCR.
            .AddInMemoryClients(ConformanceClients.Get())
            .AddInMemoryIdentityResources(TestResources.IdentityResources)
            .AddInMemoryApiResources(TestResources.ApiResources)
            .AddInMemoryApiScopes(TestResources.ApiScopes)
            .AddTestUsers(TestUsers.Users)
            .AddJwtBearerClientAuthentication()
            // FAPI 2.0 conformance requires that redirect URIs with additional query
            // parameters are accepted when the base URI matches a registered URI.
            .AddRedirectUriValidator<Fapi2RedirectUriValidator>();

        builder.Services.AddIdentityServerConfiguration(opt => { })
            .AddInMemoryClientConfigurationStore();

        // Backchannel logout HTTP client — rewrites localhost→nginx for Docker networking.
        builder.Services.AddHttpClient<ConformanceBackChannelLogoutHttpClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        builder.Services.AddTransient<IBackChannelLogoutHttpClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient(nameof(ConformanceBackChannelLogoutHttpClient));
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            var logger = sp.GetRequiredService<ILogger<ConformanceBackChannelLogoutHttpClient>>();
            return new ConformanceBackChannelLogoutHttpClient(client, logger);
        });

        builder.AddIdentityServerUI();

        // Local API authentication for the FAPI2 resource endpoint.
        // Validates tokens issued by the local IS instance and handles DPoP-bound
        // access tokens (Authorization: DPoP <token>).
        builder.Services.AddLocalApiAuthentication();
        builder.Services.Configure<LocalApiAuthenticationOptions>(
            IdentityServerConstants.LocalApi.AuthenticationScheme,
            options =>
            {
                options.TokenMode = LocalApiTokenMode.DPoPAndBearer;
                // FAPI2 tokens only carry openid scope; don't require IdentityServerApi scope.
                options.ExpectedScope = null;
            });

        return builder.Build();
    }

    internal static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();

        app.MapHealthChecks("/health");
        app.MapRazorPages().RequireAuthorization();
        app.MapDynamicClientRegistration().AllowAnonymous();

        // Log marker endpoint — the test harness calls this at the start and end of
        // each module so container logs can be correlated to specific tests.
        app.MapGet("/conformance/log-marker", (string message, ILogger<Program> logger) =>
        {
            logger.LogWarning("===== {Message} =====", message);
            return Results.Ok();
        }).AllowAnonymous();

        // Protected resource endpoint for FAPI2 conformance tests.
        // The conformance suite calls this with a DPoP-bound access token to verify
        // sender-constraining works end-to-end.
        app.MapGet("/fapi2/resource", () => Results.Ok(new { sub = "conformance-resource" }))
            .RequireAuthorization(IdentityServerConstants.LocalApi.PolicyName);

        return app;
    }
}
