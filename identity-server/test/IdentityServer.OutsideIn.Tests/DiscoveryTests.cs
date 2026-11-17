// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.OutsideIn.Tests;

public sealed class DiscoveryTests
{
    [Theory, Matrix(Dimensions.Stores | Dimensions.Caching | Dimensions.Spaces)]
    public async Task Discovery_document(Permutation p)
    {
        await using var env = await TestEnvironment.StartAsync(p, env =>
            env.SetupPerIssuer(issuer => issuer));

        var browser = env.CreateBrowser();

        await env.VerifyScenario(async (issuer, _) =>
            await browser.GetAsync(issuer.DiscoveryUrl));
    }
}
