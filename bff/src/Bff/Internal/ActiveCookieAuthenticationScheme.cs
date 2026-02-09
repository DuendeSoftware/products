// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Internal;

/// <summary>
/// Centralizes the logic for determining if the cookie authentication scheme should be configured based on the currently selected frontend and the default authentication scheme.
/// </summary>
internal sealed class ActiveCookieAuthenticationScheme(FrontendSelector frontendSelector, IOptions<AuthenticationOptions> authOptions)
{
    private readonly Scheme? _defaultAuthenticationScheme = Scheme.ParseOrDefault(authOptions.Value.DefaultAuthenticateScheme ?? authOptions.Value.DefaultScheme);

    /// <summary>
    /// Determines if the cookie authentication scheme should be configured based on the provided scheme name.
    /// </summary>
    /// <param name="schemeName"></param>
    /// <returns></returns>
    public bool ShouldConfigureScheme(Scheme? schemeName) =>

        // Either the currently selected scheme is the default scheme
        _defaultAuthenticationScheme == schemeName ||

        // Or it's actually a scheme for a specific frontend. 
        (frontendSelector.TryGetFrontendByCookieScheme(schemeName, out _));
}
