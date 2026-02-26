// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal record SamlSigninSuccess
{
    private SamlSigninSuccess(Uri redirectUri)
    {
        RedirectUri = redirectUri;
        SuccessType = SamlSigninSuccessType.Redirect;
    }

    private SamlSigninSuccess(SamlResponse response)
    {
        SamlResponse = response;
        SuccessType = SamlSigninSuccessType.Response;
    }

    public SamlSigninSuccessType SuccessType { get; private set; }
    public Uri RedirectUri { get; private set; } = null!;
    public SamlResponse SamlResponse { get; private set; } = null!;

    public static SamlSigninSuccess CreateRedirectSuccess(Uri redirectUri) => new(redirectUri);

    public static SamlSigninSuccess CreateResponseSuccess(SamlResponse samlResponse) => new(samlResponse);
}

internal enum SamlSigninSuccessType
{
    Redirect,
    Response
}
