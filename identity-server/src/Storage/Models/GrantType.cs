// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.Models;

public static class GrantType
{
    public const string Implicit = "implicit";
    public const string Hybrid = "hybrid";
    public const string AuthorizationCode = "authorization_code";
    public const string ClientCredentials = "client_credentials";
    public const string ResourceOwnerPassword = "password";
    public const string DeviceFlow = "urn:ietf:params:oauth:grant-type:device_code";
    public const string RefreshToken = "refresh_token";
}
