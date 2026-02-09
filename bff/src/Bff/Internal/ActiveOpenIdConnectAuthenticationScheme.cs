// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Internal;

/// <summary>
/// Centralizes the logic for determining if the OpenID Connect authentication scheme should be configured based on the currently selected frontend and the default authentication scheme.
/// </summary>
/// <param name="frontendSelector"></param>
/// <param name="authOptions"></param>
internal sealed class ActiveOpenIdConnectAuthenticationScheme(
    FrontendSelector frontendSelector,
    IOptions<AuthenticationOptions> authOptions)
{
    private readonly Scheme? _defaultAuthenticationScheme = Scheme.ParseOrDefault(authOptions.Value.DefaultChallengeScheme ?? authOptions.Value.DefaultScheme);

    /// <summary>
    /// Determines if the OpenID Connect authentication scheme should be configured based on the provided scheme name.
    /// </summary>
    /// <param name="schemeName"></param>
    /// <returns></returns>
    public bool ShouldConfigureScheme(Scheme? schemeName) =>

        // Either the currently selected scheme is the default scheme
        (_defaultAuthenticationScheme == schemeName)
            // Or it's the correct scheme for the currently selected frontend
            || frontendSelector.TryGetFrontendByOidcScheme(schemeName, out _);

}
