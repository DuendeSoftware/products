// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Tests.TestInfra;

public class TestDataBuilder(TestData the)
{
    public readonly TestData The = the;

    public BffFrontend BffFrontend() =>
        new()
        {
            Name = The.FrontendName,
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration,
        };

    public BffFrontend BffFrontendWithSelectionCriteria() =>
        new()
        {
            Name = The.FrontendName,
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration,
            IndexHtmlUrl = The.Url,
            SelectionCriteria = FrontendSelectionCriteria(),
            Proxy = BffProxy()
        };

    public BffProxy BffProxy() => new()
    {
        RemoteApis = [RemoteApi()]
    };

    public RemoteApi RemoteApi() => new()
    {
        LocalPath = The.Path,
        TargetUri = The.Url,
        RequiredTokenType = The.RequiredTokenType,
        Parameters = BffUserAccessTokenParameters(),
        AccessTokenRetrieverType = The.TokenRetrieverType
    };

    public BffFrontend NeverMatchingFrontEnd() =>
        new()
        {
            Name = BffFrontendName.Parse("should not be found"),
            SelectionCriteria = new FrontendSelectionCriteria()
            {
                MatchingOrigin = Origin.Parse("https://will-not-be-found"),
                MatchingPath = "/will_not_be_found",
            }
        };

    public RouteConfig RouteConfig() => new()
    {
        RouteId = The.RouteId,
        ClusterId = The.ClusterId,

        Match = new RouteMatch
        {
            Path = $"{The.Path}/{{**catch-all}}"
        }
    };

    public ClusterConfig ClusterConfig(ApiHost api) => new()
    {
        ClusterId = The.ClusterId,

        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "destination1", new DestinationConfig { Address = api.Url().ToString() } }
        }
    };


    public OidcConfiguration OidcConfiguration() =>
        new()
        {
            ClientId = The.ClientId,
            ClientSecret = The.ClientSecret,
            Authority = The.Authority,
            ResponseMode = The.ResponseMode,
            ResponseType = The.ResponseType,
            GetClaimsFromUserInfoEndpoint = true,
            MapInboundClaims = false,
            SaveTokens = true,
            Scope = [The.Scope],
            CallbackPath = The.CallbackPath,
        };

    public BffFrontendConfiguration BffFrontendConfiguration() =>
        new()
        {
            IndexHtmlUrl = The.Url,
            MatchingOrigin = The.Origin.ToString(),
            MatchingPath = The.Path,
            Oidc = OidcConfiguration(),
            RemoteApis = [RemoteApiConfig()],
            Cookies = CookieConfiguration()
        };

    public RemoteApiConfig RemoteApiConfig() => new()
    {
        LocalPath = The.Path,
        TargetUri = The.Url,
        RequiredTokenType = The.RequiredTokenType,
        TokenRetrieverTypeName = typeof(TestTokenRetriever).AssemblyQualifiedName,
        UserAccessTokenParameters = UserAccessTokenParameters()
    };

    public UserAccessTokenParameters UserAccessTokenParameters() =>
        new()
        {
            ChallengeScheme = The.Scheme,
            SignInScheme = The.Scheme,
            ForceRenewal = true,
            Resource = The.Resource
        };


    public BffUserAccessTokenParameters BffUserAccessTokenParameters() =>
        new()
        {
            SignInScheme = The.Scheme,
            ChallengeScheme = The.Scheme,
            ForceRenewal = true,
            Resource = The.Resource
        };

    public FrontendSelectionCriteria FrontendSelectionCriteria() => new()
    {
        MatchingOrigin = The.Origin,
        MatchingPath = The.Path,
    };

    public ClaimsPrincipal ClaimsPrincipal() => new(new ClaimsIdentity(new List<Claim>()
    {
        new(JwtClaimTypes.Name, The.UserName),
        new(JwtClaimTypes.Subject, The.Sub)
    }, "test", "name", "role"));

    public CookieConfiguration? CookieConfiguration() => new()
    {
        Name = The.CookieName,
        Path = The.Path,
        Domain = The.DomainName.Host,
        HttpOnly = true,
        MaxAge = The.MaxAge,
        SecurePolicy = CookieSecurePolicy.Always,
        SameSite = SameSiteMode.Strict
    };

    public CookieAuthenticationOptions CookieAuthenticationOptions() => new()
    {
        Cookie = new CookieBuilder
        {
            Name = The.CookieName,
            Path = The.Path,
            Domain = The.DomainName.Host,
            HttpOnly = true,
            MaxAge = The.MaxAge,
            SecurePolicy = CookieSecurePolicy.Always,
            SameSite = SameSiteMode.Strict
        },
    };
}
