// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
namespace Duende.AspNetCore.Authentication.JwtBearer.TestFramework;

public class IdentityServerHost : GenericHost
{
    public IdentityServerHost(string baseAddress = "https://identityserver")
        : base(baseAddress)
    {
        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    public List<Client> Clients { get; } = new();
    public List<IdentityResource> IdentityResources { get; } =
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ];
    public List<ApiScope> ApiScopes { get; } =
    [
        new ApiScope("scope1")
    ];
    public List<ApiResource> ApiResources { get; } =
    [
        new ApiResource("urn:api1"),
        new ApiResource("urn:api2")
    ];

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();

        services.AddIdentityServer(options =>
            {
                options.EmitStaticAudienceClaim = true;

                // Artificially low durations to force retries
                options.DPoP.ServerClockSkew = TimeSpan.Zero;
                options.DPoP.ProofTokenValidityDuration = TimeSpan.FromSeconds(1);
            })
            .AddInMemoryClients(Clients)
            .AddInMemoryIdentityResources(IdentityResources)
            .AddInMemoryApiResources(ApiResources)
            .AddInMemoryApiScopes(ApiScopes);
    }

    private void Configure(IApplicationBuilder app)
    {
        app.UseRouting();

        app.UseIdentityServer();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/account/login", context =>
            {
                return Task.CompletedTask;
            });

            endpoints.MapGet("/account/logout", async context =>
            {
                // signout as if the user were prompted
                await context.SignOutAsync();

                var logoutId = context.Request.Query["logoutId"];
                var interaction = context.RequestServices.GetRequiredService<IIdentityServerInteractionService>();

                var signOutContext = await interaction.GetLogoutContextAsync(logoutId);

                context.Response.Redirect(signOutContext.PostLogoutRedirectUri ?? "/");
            });
        });
    }

    public async Task CreateIdentityServerSessionCookieAsync(string sub, string? sid = null)
    {
        var props = new AuthenticationProperties();

        if (!string.IsNullOrWhiteSpace(sid))
        {
            props.Items.Add("session_id", sid);
        }

        await IssueSessionCookieAsync(props, new Claim("sub", sub));
    }

    public string CreateIdToken(string sub, string clientId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _baseAddress,
            Audience = clientId,
            Claims = new Dictionary<string, object>
            {
                { "sub", sub }
            }
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }
}
