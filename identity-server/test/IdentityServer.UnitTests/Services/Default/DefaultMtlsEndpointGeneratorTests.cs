// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services.Default;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultMtlsEndpointGeneratorTests
{
    private const string Origin = "https://identity.example.com";

    private static IOptions<IdentityServerOptions> CreateOptions(string mtlsDomain)
    {
        var options = new IdentityServerOptions
        {
            MutualTls =
            {
                DomainName = mtlsDomain
            }
        };
        return Options.Create(options);
    }

    [Fact]
    public void GetMtlsEndpointPath_PathBased_ReturnsPathBasedMtlsUrl()
    {
        var serverUrls = new MockServerUrls { Origin = Origin };
        var options = CreateOptions(""); // path-based
        var generator = new DefaultMtlsEndpointGenerator(serverUrls, options);

        var result = generator.GetMtlsEndpointPath(IdentityServerConstants.ProtocolRoutePaths.Token);

        result.ShouldBe("https://identity.example.com/connect/mtls/token");
    }

    [Fact]
    public void GetMtlsEndpointPath_DomainBased_ReturnsDomainBasedMtlsUrl()
    {
        var serverUrls = new MockServerUrls { Origin = Origin };
        var options = CreateOptions("mtls.example.com"); // domain-based

        var generator = new DefaultMtlsEndpointGenerator(serverUrls, options);

        var result = generator.GetMtlsEndpointPath(IdentityServerConstants.ProtocolRoutePaths.Token);

        result.ShouldBe("https://mtls.example.com/connect/token");
    }

    [Fact]
    public void GetMtlsEndpointPath_SubDomainBased_ReturnsSubDomainBasedMtlsUrl()
    {
        var serverUrls = new MockServerUrls { Origin = Origin };
        var options = CreateOptions("mtls"); // sub-domain based

        var generator = new DefaultMtlsEndpointGenerator(serverUrls, options);

        var result = generator.GetMtlsEndpointPath(IdentityServerConstants.ProtocolRoutePaths.Token);

        result.ShouldBe("https://mtls.identity.example.com/connect/token");
    }
}
