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
    public DpopRemoteEndpointTests(ITestOutputHelper output) : base(output)
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        jsonWebKey.Alg = "PS256";
        var jwk = JsonSerializer.Serialize(jsonWebKey);

        BffHost.OnConfigureServices += svcs =>
        {
            svcs.PostConfigure<BffOptions>(opts =>
            {
                opts.DPoPJsonWebKey = jwk;
            });
        };
    }

    [Fact]
    public async Task test_dpop()
    {
        ApiResponse apiResult = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/api_client/test")
        );

        apiResult.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        apiResult.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
    }
}
