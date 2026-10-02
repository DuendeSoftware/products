// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Introspection.Setup;

internal class Scopes
{
    public static IEnumerable<IdentityResource> GetIdentityScopes() => new IdentityResource[]
    {
        new IdentityResources.OpenId(),
        new IdentityResources.Email(),
        new IdentityResources.Address(),
        new IdentityResource("roles", new[] { "role" })
    };

    public static IEnumerable<ApiResource> GetApis() => new ApiResource[]
        {
            new ApiResource
            {
                Name = "api1",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "api1" },
                UserClaims = { "role", "address" }
            },
            new ApiResource
            {
                Name = "api2",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "api2" }
            },
            new ApiResource
            {
                Name = "api3",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "api3-a", "api3-b" }
            },
            // Dedicated API resources for telemetry tests. These callers must not be shared with any
            // other test in the assembly: telemetry measurements are captured via a process-wide Meter,
            // so a caller value reused by another (possibly parallel) test would make the measurement
            // count for these tests non-deterministic. See PR #3565 review discussion.
            //
            // "introspection-telemetry-valid-active-token" needs its own dedicated scope: the
            // introspection response generator only reports a token as active to an API caller when
            // the caller's ApiResource.Scopes intersects with the token's scopes (see
            // IntrospectionResponseGenerator.AreExpectedScopesPresentAsync). Reusing "api1" here would
            // work but would also add this resource to the "aud" claim of every token issued for the
            // "api1" scope, breaking unrelated tests that assert on "aud". A scope used only by this
            // resource keeps that isolated.
            new ApiResource
            {
                Name = "introspection-telemetry-valid-active-token",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "introspection-telemetry-valid-active-token" }
            },
            // Not associated with any Scopes: this resource is only used to authenticate an
            // introspection call for an already-invalid token, which never reaches the scope check.
            new ApiResource
            {
                Name = "introspection.telemetry.validation-error",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                }
            }
        };
    public static IEnumerable<ApiScope> GetScopes() => new ApiScope[]
        {
            new ApiScope
            {
                Name = "api1"
            },
            new ApiScope
            {
                Name = "api2"
            },
            new ApiScope
            {
                Name = "api3-a"
            },
            new ApiScope
            {
                Name = "api3-b"
            },
            new ApiScope
            {
                Name = "introspection-telemetry-valid-active-token"
            }
        };
}
