// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Licensing;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Yarp;
using Duende.Bff.Yarp.Internal;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging.Abstractions;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Tests.TestInfra;

public class TestDataBuilder(TestData the)
{
    public readonly TestData The = the;

    internal LicenseValidator LicenseValidator => new(
            logger: new NullLogger<LicenseValidator>(),
            claims: new ClaimsPrincipal(new ClaimsIdentity(AllowAllLicenseClaims())),
            timeProvider: The.Clock);

    public Claim[] AllowAllLicenseClaims()
    {
        return LicenseClaims(true, License.LicenseEdition.Bff, -1);
    }

    public Claim[] LicenseClaims(bool licensed = true, License.LicenseEdition? edition = null,
        int? numberOfLicenses = null)
    {
        List<Claim> claims = [];

        if (licensed)
        {
            claims.Add(new Claim("feature", "bff"));
        }

        if (edition != null)
        {
            claims.Add(new Claim("edition", edition.ToString()!));
        }

        if (numberOfLicenses != null)
        {
            claims.Add(new Claim("bff_frontend_limit", numberOfLicenses.ToString()!));
        }

        return claims.ToArray();
    }

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
            SelectionCriteria = FrontendSelectionCriteria()
        };

    internal ProxyBffPlugin ProxyDataExtension() =>

        new ProxyBffPlugin()
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
            Name = BffFrontendName.Parse("should_not_be_found"),
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


    internal OidcConfiguration OidcConfiguration() =>
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

    internal BffFrontendConfiguration BffFrontendConfiguration() =>
        new()
        {
            IndexHtmlUrl = The.Url,
            MatchingOrigin = The.Origin.ToString(),
            MatchingPath = The.Path,
            Oidc = OidcConfiguration(),
            Cookies = CookieConfiguration()
        };

    internal RemoteApiConfiguration RemoteApiConfiguration() => new()
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

    internal CookieConfiguration? CookieConfiguration() => new()
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

    public UserSessionsFilter UserSessionsFilter() => new()
    {
        SubjectId = The.Sub
    };

    internal FrontendProxyConfiguration FrontendProxyConfiguration() => new()
    {
        RemoteApis = [RemoteApiConfiguration()],
    };

    public UserSession UserSession() => new()
    {
        PartitionKey = The.PartitionKey,
        Key = The.UserKey,
        SessionId = "sid",
        SubjectId = "sub",
        Created = new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc),
        Renewed = new DateTime(2021, 4, 2, 10, 13, 34, DateTimeKind.Utc),
        Expires = new DateTime(2022, 5, 3, 11, 14, 35, DateTimeKind.Utc),
        Ticket = "ticket"
    };
}
