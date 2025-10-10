// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.IdentityModel;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Web;

public class AssertionService(IConfiguration configuration)
{
    private static string rsaKey =
        """
         {
             "p": "4LqAq8wFhdYCpuf6RutkfgCcQAtTKbTwjEniOuidnkVzb9FObXInIccspei91RPyLxX5d8KjeggvsKaxbSC4_eRCpT_WC1fvib8vIojyMeHX0R5cw1nlBqIm8wyu0RzLFd14p-fcI_WSECyO_FMXXhHRCWiw2TQViiUQdehNoZc",
             "kty": "RSA",
             "q": "t54zGNc9235doIVycfd5ZJpONjfv46qcezfXnqhNU_zFy4kJEj5f-egLXOnmyROrR2MWGCRcyUYMQdGiqdjM6qBq64_ELDA-9-_QgXIeoUmE1IhK9ftKOM3yZEHqEs_irY5o1JjytEiWmR9q2RARI1SuN6pMTNRDfFlSpdLfWD0",
             "d": "SCqp0Y_wxRtSAI61PkXcVs_W_SnrXjxs3lEiLsQCRnV6RMVSXKA942NZlVWqgB9v9dnZF9GSF2AdDPNhsYoMuofRYOFn-W2X0nYu29_3Uzlewq32LEIcB0Q9efNW1w_UlzuKZQSwnwjLlRPIuOs6GUb12hNBZEHH4J4nwmxk-VT7x15E5HhJPRuerDRiLyEy-w8zDUL9c6iWpffXG5-25zLLUY_M_VPPBQvBF5HXbTB4TMirYZaxLP0JNBWNHVzIqWG4dZjL3w2pfHFiL6bRsTrh3pQyA-KA-KozO-Dh-f1mGysj9auSECaPonYg69e60eeYJfDBRrfMvxo8ZqVqsQ",
             "e": "AQAB",
             "use": "sig",
             "kid": "web-0001",
             "qi": "0f00wujVRzVQImfy1QCvIMumIarWnFEoNs-FtU3fRWGKPd9i4ruXAxdxQGcdPvK1y-01sEBsYV0GeLOU4xvInvmheSWwsX4WgvuDvOSrjlfmXwMD7tnex_ynlYKAYl6jmdLLjOOS7X2qHUxuHfXUg4dwaTp2fnyjEedf-65tUQE",
             "dp": "SPTDKQLHGDfuDHlrCvMIYM-Z6kDC8ttG7IRf6XfzE5rAayCsMWPJyHF80S_J0Q70pMyhfHu3zroxoUu8dg0VgXdFG5ipyGz32uQyTSfgWMlU4xLUUqcwbwLdWjJX3pNWavbHYNso4JOso4uTr97ZyzRFhKR0JU9_XqXBvkV4Tmk",
             "alg": "PS256",
             "dq": "LzQsTqaG8HZ7-1hTI5lLS-GfWbDnqs-hisvAUrlRp9XDw59nBZmjcsuEoE5BVlAIKEIA3BP9BoFLhWAvQRrLE0ZKNmSvOeztQzATmjOMTEpqK3keTD5dxlyrg7quQkfPLm795Cmtu0st7A93mHXY8gxC_Wx6UQYAk2cjKB4d7ME",
             "n": "oTAx8S7xFwQ7gFixieULyMG9JIeNLzLkXdw7rRCRjKhJy67jPjHkbT51uDTntWc_rx7S6GoKBjJCCau1JnBS9Z9UX7d84Ado0aeLCYjZPOMRm1u0OB6kxOa46bB4-uke7fnWTQN8motNycvyXFd7kENqtkk2hmxB1wvr1WPSnJ037JqJ3-j9ZEM016GCj98_R_aJtJQg2jhv9rGJMIRdr2JhzAjKFg4m6W_MRSdxzrEtF3mNGGIpIPRw8_bH5uvQG6dIUfpOWr1IPbmIzbk5JOAwrtthG_1v8J-8QJ8Md5IJgMvKBTow5pX2YTE632vHVZedL3lhopehDQJzqpRo-w"
         }
        """;

    public string CreateClientToken()
    {
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            "web",
            configuration["is-host"],
            new List<Claim>()
            {
                new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.Subject, "web"),
                new Claim(JwtClaimTypes.IssuedAt, ((DateTimeOffset) now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            },
            now,
            now.AddMinutes(1),
            new SigningCredentials(new JsonWebKey(rsaKey), "PS256")
        );

        token.Header[JwtClaimTypes.TokenType] = "client-authentication+jwt";

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();

        return tokenHandler.WriteToken(token);
    }

    public string SignAuthorizationRequest(OpenIdConnectMessage message)
    {
        var now = DateTime.UtcNow;

        var claims = new List<Claim>();
        foreach (var parameter in message.Parameters)
        {
            claims.Add(new Claim(parameter.Key, parameter.Value));
        }

        var token = new JwtSecurityToken(
            "web",
            configuration["is-host"],
            claims,
            now,
            now.AddMinutes(1),
            new SigningCredentials(new JsonWebKey(rsaKey), "PS256")
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.OutboundClaimTypeMap.Clear();

        return tokenHandler.WriteToken(token);
    }
}
