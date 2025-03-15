// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Tests.TestFramework;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Tests.TestHosts;

public class IdentityServerHost : GenericHost
{
    public IdentityServerHost(WriteTestOutput output, string baseAddress = "https://identityserver")
        : base(output, baseAddress)
    {
        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    public List<Client> Clients { get; set; } = new();

    public List<IdentityResource> IdentityResources { get; set; } = new()
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
        };

    public List<ApiScope> ApiScopes { get; set; } = new();

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();

        services.AddLogging(logging =>
        {
            logging.AddFilter("Duende", LogLevel.Debug);
        });

        services.AddIdentityServer(options =>
        {
            options.EmitStaticAudienceClaim = true;
        })
            .AddInMemoryClients(Clients)
            .AddInMemoryIdentityResources(IdentityResources)
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
            endpoints.MapGet("/__token", async (ITokenService tokens) =>
            {
                var token = new Token(IdentityServerConstants.TokenTypes.AccessToken)
                {
                    Issuer = "https://identityserver",
                    Lifetime = Convert.ToInt32(TimeSpan.FromDays(1).TotalSeconds),
                    CreationTime = DateTime.UtcNow,

                    Claims = new List<Claim>
                    {
                            new("client_id", "fake-client"),
                            new("sub", "123")
                    },
                    Audiences = new List<string>
                    {
                            "https://identityserver/resources"
                    },
                    AccessTokenType = AccessTokenType.Jwt
                };

                return await tokens.CreateSecurityTokenAsync(token);
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

    public async Task<BearerTokenResult> CreateJwtAccessTokenAsync()
    {
        var response = await BrowserClient.GetAsync(Url("__token"));
        var accessToken = await response.Content.ReadAsStringAsync();
        return new BearerTokenResult(accessToken);
    }
}
