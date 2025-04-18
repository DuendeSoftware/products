// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),

        // 1. The email resource must be added to the system. 
        // This maps the email scope to the email claim. 
        new IdentityResources.Email()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        // 3. The API scope, requested by the BFF needs to also require the email claim. 
        // Basically, you're telling that, if you want to call this api, it also needs the email claim
        new("api", ["name", "email"]),

        new("scope-for-isolated-api", ["name"]),
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new("urn:isolated-api", "isolated api")
        {
            RequireResourceIndicator = true,
            Scopes = { "scope-for-isolated-api" }
        }
    ];
}
