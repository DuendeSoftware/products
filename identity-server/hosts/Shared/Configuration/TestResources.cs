// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace IdentityServerHost.Configuration;

public static class TestResources
{
    // identity resources represent identity data about a user that can be requested via the scope parameter (OpenID Connect)
    public static readonly IEnumerable<IdentityResource> IdentityResources =
        [
            // some standard scopes from the OIDC spec
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),

            // custom identity resource with some consolidated claims
            new IdentityResource("custom.profile", [JwtClaimTypes.Name, JwtClaimTypes.Email, "location", JwtClaimTypes.Address])
        ];

    // API scopes represent values that describe scope of access and can be requested by the scope parameter (OAuth)
    public static readonly IEnumerable<ApiScope> ApiScopes =
        [
            // local API scope
            new ApiScope(IdentityServerConstants.LocalApi.ScopeName),

            // resource specific scopes
            new ApiScope("resource1.scope1"),
            new ApiScope("resource1.scope2"),

            new ApiScope("resource2.scope1"),
            new ApiScope("resource2.scope2"),

            new ApiScope("resource3.scope1"),
            new ApiScope("resource3.scope2"),
                
            // a scope without resource association
            new ApiScope("scope3"),
            new ApiScope("scope4"),
                
            // a scope shared by multiple resources
            new ApiScope("shared.scope"),

            // a parameterized scope
            new ApiScope("transaction", "Transaction")
            {
                Description = "Some Transaction"
            }
        ];

    // API resources are more formal representation of a resource with processing rules and their scopes (if any)
    public static readonly IEnumerable<ApiResource> ApiResources =
        [
            new ApiResource("urn:resource1", "Resource 1")
            {
                Description = "Something very long and descriptive",
                ApiSecrets = { new Secret("secret".Sha256()) },

                Scopes = { "resource1.scope1", "resource1.scope2", "shared.scope" }
            },

            new ApiResource("urn:resource2", "Resource 2")
            {
                Description = "Something very long and descriptive",
                ApiSecrets = { new Secret("secret".Sha256()) },

                // additional claims to put into access token
                UserClaims =
                {
                    JwtClaimTypes.Name,
                    JwtClaimTypes.Email
                },

                Scopes = { "resource2.scope1", "resource2.scope2", "shared.scope" }
            },

            new ApiResource("urn:resource3", "Resource 3 (isolated)")
            {
                ApiSecrets = { new Secret("secret".Sha256()) },

                RequireResourceIndicator = true,
                Scopes = { "resource3.scope1", "resource3.scope2", "shared.scope" }
            }
        ];
}
