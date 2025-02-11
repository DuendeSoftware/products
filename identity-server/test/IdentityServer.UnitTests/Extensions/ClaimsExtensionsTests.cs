using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using FluentAssertions;

namespace UnitTests.Extensions;

public class ClaimsExtensionsTests
{

    [Theory]
    [InlineData(System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.Json)]
    [InlineData(IdentityServerConstants.ClaimValueTypes.Json)]
    public void TestName(string claimType)
    {
        var payload =
        """
        {
            "test": "value"
        }
        """;
        Claim[] claims = [new Claim("claim", payload, claimType)];

        var result = claims.ToClaimsDictionary();

        result["claim"].Should().BeOfType<JsonElement>();
    }
}