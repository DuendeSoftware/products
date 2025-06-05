// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Benchmarks.Hosts;

public class IdentityServerHost : Host
{

    public IdentityServerHost()
    {
        OnConfigureServices += services =>
        {

            var identityServer = services.AddIdentityServer(options =>
                {
                    options.EmitStaticAudienceClaim = true;
                    options.UserInteraction.CreateAccountUrl = "/account/create";
                })

                .AddInMemoryClients(Clients)
                .AddInMemoryIdentityResources(IdentityResources)
                .AddInMemoryApiScopes(ApiScopes);

            identityServer.AddBackChannelLogoutHttpClient();
        };

        OnConfigure += app =>
        {
            app.UseRouting();

            app.UseIdentityServer();
            app.UseAuthorization();

            app.MapGet("/account/login", async ctx =>
            {
                await ctx.SignInAsync(UserToSignIn);
            });
        };
    }

    public List<Client> Clients { get; set; } = new();
    public List<IdentityResource> IdentityResources { get; set; } = new()
    {
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
    };

    public List<ApiScope> ApiScopes { get; set; } = [new ApiScope("api")];

    public ClaimsPrincipal UserToSignIn { get; set; } = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim("name", "bob"),
        new Claim("sub", "bob"),
    ], "test", "name", "role"));

}
