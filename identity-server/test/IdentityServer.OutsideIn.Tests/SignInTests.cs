// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.OutsideIn.Tests;

public sealed class SignInTests
{
    [Theory, Matrix(Dimensions.Stores | Dimensions.Caching | Dimensions.Users | Dimensions.Spaces)]
    public async Task Sign_in_to_web_app(Permutation p)
    {
        await using var env = await TestEnvironment.StartAsync(p, env =>
        {
            env.SetupIdentityServer(ids => ids.WithPasswordLogin());
            return env.SetupPerIssuer(issuer =>
                (alice: issuer.TestUsers.Create("alice"),
                 clientApp: env.AddClientApp("web", app => app.UseAuthority(issuer).WithUserInfoEndpoint())));
        });

        await env.SetupData(async (issuer, r) =>
        {
            await issuer.Storage.AddDefaultsAsync();
            await issuer.Storage.AddUserAsync(r.alice);
            await issuer.Storage.AddClientAsync(issuer.TestClients.CodeFlow(r.clientApp));
        });

        var browser = env.CreateBrowser();

        await env.VerifyScenario(async (issuer, r) =>
        {
            var page = await browser.OpenAsync(r.clientApp.SignInUrl);

            var login = page.ShouldBe<LoginPage>(on: issuer);
            page = await login.SignInAsAsync(r.alice);

            return page.ShouldBe<AppPage>(on: r.clientApp).ShouldBeSignedInAs(r.alice);
        });
    }

    [Theory, Matrix(Dimensions.Stores | Dimensions.Caching | Dimensions.Spaces)]
    public async Task Federated_sign_in(Permutation p)
    {
        await using var env = await TestEnvironment.StartAsync(p, env =>
        {
            env.SetupIdentityServer(ids => ids.WithExternalLogin());
            return env.SetupPerIssuer(issuer =>
            {
                var alice = issuer.TestUsers.Create("alice");
                var upstream = env.AddRemoteIdentityServer("upstream", ids => ids.WithPasswordLogin());
                var clientApp = env.AddClientApp("web", app => app.UseAuthority(issuer).WithUserInfoEndpoint());
                return (alice, upstream, clientApp);
            });
        });

        await env.SetupData(async (issuer, r) =>
        {
            await issuer.Storage.AddDefaultsAsync();
            await issuer.Storage.AddClientAsync(issuer.TestClients.CodeFlow(r.clientApp));
            await issuer.Storage.AddRemoteIdpAsync(r.upstream, Protocol.Oidc);
            await r.upstream.Storage.AddUserAsync(r.alice);
        });

        var browser = env.CreateBrowser();

        await env.VerifyScenario(async (_, r) =>
        {
            var page = await browser.OpenAsync(r.clientApp.SignInUrl);   // the only provider: straight to the upstream

            var login = page.ShouldBe<LoginPage>(on: r.upstream);
            page = await login.SignInAsAsync(r.alice);

            return page.ShouldBe<AppPage>(on: r.clientApp).ShouldBeSignedInAs(r.alice);
        });
    }
}
