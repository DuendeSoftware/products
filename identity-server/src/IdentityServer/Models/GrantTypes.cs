// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityModel;

#pragma warning disable 1591

namespace Duende.IdentityServer.Models;

public static class GrantTypes
{
    public static ICollection<string> Implicit =>
        new[] { GrantType.Implicit };

    public static ICollection<string> ImplicitAndClientCredentials =>
        new[] { GrantType.Implicit, GrantType.ClientCredentials };

    public static ICollection<string> Code =>
        new[] { GrantType.AuthorizationCode };

    public static ICollection<string> CodeAndClientCredentials =>
        new[] { GrantType.AuthorizationCode, GrantType.ClientCredentials };

    public static ICollection<string> Hybrid =>
        new[] { GrantType.Hybrid };

    public static ICollection<string> HybridAndClientCredentials =>
        new[] { GrantType.Hybrid, GrantType.ClientCredentials };

    public static ICollection<string> ClientCredentials =>
        new[] { GrantType.ClientCredentials };

    public static ICollection<string> ResourceOwnerPassword =>
        new[] { GrantType.ResourceOwnerPassword };

    public static ICollection<string> ResourceOwnerPasswordAndClientCredentials =>
        new[] { GrantType.ResourceOwnerPassword, GrantType.ClientCredentials };

    public static ICollection<string> DeviceFlow =>
        new[] { GrantType.DeviceFlow };

    public static ICollection<string> Ciba =>
        new[] { OidcConstants.GrantTypes.Ciba };
}
