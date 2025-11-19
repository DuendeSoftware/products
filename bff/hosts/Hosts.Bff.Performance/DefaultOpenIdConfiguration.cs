// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Hosts.Bff.Performance.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

public static class DefaultOpenIdConfiguration
{
    public static void Apply(OpenIdConnectOptions options, BffSettings settings)
    {
        options.Authority = settings.IdentityServerUrl.ToString();

        // confidential client using code flow + PKCE
        options.ClientId = "bff.perf";
        options.ClientSecret = "secret";
        options.ResponseType = "code";
        options.ResponseMode = "query";

        options.MapInboundClaims = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        // request scopes + refresh tokens
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("api");
        options.Scope.Add("offline_access");
    }
}
