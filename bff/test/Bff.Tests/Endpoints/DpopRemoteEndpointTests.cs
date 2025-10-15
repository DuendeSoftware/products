// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.Bff.Tests.Endpoints;

public class DpopRemoteEndpointTests : BffIntegrationTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public DpopRemoteEndpointTests(ITestOutputHelper output) : base(output)
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        jsonWebKey.Alg = "PS256";
        var jwk = JsonSerializer.Serialize(jsonWebKey);

        BffHost.OnConfigureServices += services =>
        {
            services.PostConfigure<BffOptions>(opts =>
            {
                opts.DPoPJsonWebKey = jwk;
            });
        };
    }

    [Fact]
    public async Task test_dpop()
    {
        ApiResponse apiResult = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/api_client/test"), ct: _ct);

        apiResult.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        apiResult.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
    }
}
