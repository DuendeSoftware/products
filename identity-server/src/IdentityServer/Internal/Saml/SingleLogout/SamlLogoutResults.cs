// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal record SamlLogoutSuccess
{
    private SamlLogoutSuccess(IEndpointResult result) => Result = result;

    public IEndpointResult Result { get; private set; }

    public static SamlLogoutSuccess CreateResponse(LogoutResponse logoutResponse) =>
        new(logoutResponse);

    public static SamlLogoutSuccess CreateRedirect(Uri redirectUri) => new(new RedirectResult(redirectUri));
}
