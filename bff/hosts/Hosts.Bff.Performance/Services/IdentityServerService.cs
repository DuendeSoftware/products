// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Hosts.Bff.Performance.Services;

public class IdentityServerService(IOptions<IdentityServerSettings> settings, IConfiguration config) : BackgroundService
{
    public IdentityServerSettings Settings { get; } = settings.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddServiceDefaults();
        // Configure Kestrel to listen on the specified Uri
        builder.WebHost.UseUrls(Settings.IdentityServerUrl);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
        });

        builder.Services.AddAuthorization();
        builder.Services.AddHttpLogging();

        var isBuilder = builder.Services.AddIdentityServer(options =>
                {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;

                    options.EmitStaticAudienceClaim = true;
                    options.KeyManagement.KeyPath = Path.GetTempPath();
                })
                .AddTestUsers([new TestUser()
                {
                    SubjectId = "bob",
                    Username = "bob",
                    Password = "bob",
                    Claims = [
                        new Claim(JwtClaimTypes.Name, "Bob Smith"),
                        new Claim(JwtClaimTypes.GivenName, "Bob"),
                        new Claim(JwtClaimTypes.FamilyName, "Smith"),
                        new Claim(JwtClaimTypes.Email, "bob@duende.com")
                    ],
                    IsActive = true

                }])
            ;

        // in-memory, code config
        isBuilder.AddInMemoryIdentityResources(Config.IdentityResources);
        isBuilder.AddInMemoryApiScopes(Config.ApiScopes);

        var bffUrls = config.AsEnumerable()
            .Where(x => x.Key.StartsWith("BFFURL"))
            .Select(x => x.Value)
            .OfType<string>();

        isBuilder.AddInMemoryClients([new Client
        {
            ClientId = "bff.perf",
            ClientSecrets = [new Secret("secret".Sha256())],
            RedirectUris = bffUrls.Select(x => x.TrimEnd('/') + "/signin-oidc").ToArray(),
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "api" },
            AllowedGrantTypes =
            {
                GrantType.AuthorizationCode,
                GrantType.ClientCredentials,
                OidcConstants.GrantTypes.TokenExchange
            },

            RefreshTokenExpiration = TokenExpiration.Absolute,
            AbsoluteRefreshTokenLifetime = 60,
            AccessTokenLifetime = 15 // Force refresh

        }]);
        isBuilder.AddInMemoryApiResources(Config.ApiResources);

        var app = builder.Build();
        app.UseForwardedHeaders();

        app.UseHttpLogging();
        app.UseDeveloperExceptionPage();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();

        app.MapGet("/", () => "identity server");

        app.MapGet("/account/login", async ctx =>
        {
            var props = new AuthenticationProperties();
            await ctx.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(JwtClaimTypes.Subject, "bob"),
                    new Claim(JwtClaimTypes.Name, "bob")
                ],
                "login", "name", "role")), props);
        });

        app.MapGet("/account/logout", async ctx =>
        {
            // signout as if the user were prompted
            await ctx.SignOutAsync();

            var logoutId = ctx.Request.Query["logoutId"];
            var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();

            var signOutContext = await interaction.GetLogoutContextAsync(logoutId);

            ctx.Response.Redirect(signOutContext.PostLogoutRedirectUri ?? "/");
        });

        return app.RunAsync(stoppingToken);
    }

}
