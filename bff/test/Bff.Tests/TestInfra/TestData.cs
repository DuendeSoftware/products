// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Duende.Bff.Tests.TestInfra;

public class TestData
{
    public Uri Authority = new Uri("https://" + PropertyName());
    public string ClientSecret = PropertyName();

    // By default, clientID == frontend name
    public string ClientId = PropertyName(nameof(FrontendName));
    public BffFrontendName FrontendName = BffFrontendName.Parse(PropertyName());

    public string ResponseMode = OpenIdConnectResponseMode.Query;
    public string ResponseType = OpenIdConnectResponseType.Code;

    public Uri DomainName = new Uri($"https://{PropertyName()}");
    public Uri Url = new Uri($"https://{PropertyName()}");
    public Origin Origin = Origin.Parse($"https://{PropertyName()}:1234");
    public int Port = 1234;
    public PathString Path = new PathString($"/{PropertyName()}");
    public PathString SubPath = new PathString($"/{PropertyName(nameof(Path))}/{PropertyName()}");
    public string Scope = PropertyName();
    public string RouteId = PropertyName();
    public string ClusterId = PropertyName();
    public string UserName = PropertyName();
    public string Sub = PropertyName();
    public DPoPProofKey DPoPJsonWebKey = BuildDPoPJsonWebKey();
    public string Sid = PropertyName();
    public string CookieName = PropertyName();
    public TimeSpan? MaxAge = TimeSpan.FromDays(10);
    public RequiredTokenType RequiredTokenType = RequiredTokenType.UserOrClient;
    public Uri CallbackPath => new Uri("/" + PropertyName(), UriKind.Relative);

    public FakeTimeProvider Clock = new FakeTimeProvider(DateTimeOffset.UtcNow);

    public DateTimeOffset CurrentTime => Clock.GetUtcNow();

    public Type TokenRetrieverType = typeof(TestTokenRetriever);

    public Resource Resource = Resource.Parse(PropertyName());

    public Scheme Scheme = Scheme.Parse(PropertyName());

    private static DPoPProofKey BuildDPoPJsonWebKey()
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        jsonWebKey.Alg = "PS256";
        var jwk = JsonSerializer.Serialize(jsonWebKey);
        return DPoPProofKey.Parse(jwk);
    }

    private static string PropertyName([CallerMemberName] string? name = null) => $"_{name}_";

    public OpenIdConnectOptions OpenIdConnectOptions;
    public Action<OpenIdConnectOptions> DefaultOpenIdConnectConfiguration;
    public TestData(Uri? authority = null)
    {
        if (authority != null)
        {
            Authority = authority;
        }

        OpenIdConnectOptions = new OpenIdConnectOptions();

        DefaultOpenIdConnectConfiguration = (OpenIdConnectOptions opt) =>
        {
            opt.Authority = Authority.ToString();
            opt.ClientId = ClientId;
            opt.ClientSecret = ClientSecret;
            opt.ResponseType = ResponseType;
            opt.ResponseMode = ResponseMode;
            opt.MapInboundClaims = false;
            opt.GetClaimsFromUserInfoEndpoint = true;
            opt.SaveTokens = true;
            opt.Scope.Add("openid");
            opt.Scope.Add("profile");
            opt.Scope.Add(Scope);
        };

        DefaultOpenIdConnectConfiguration(OpenIdConnectOptions);

    }
}
