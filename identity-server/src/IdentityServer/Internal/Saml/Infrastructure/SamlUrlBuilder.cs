// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal class SamlUrlBuilder(IServerUrls urls,
    IOptions<IdentityServerOptions> identityServerOptions,
    IOptions<SamlOptions> samlOptions)
{
    private readonly SamlUserInteractionOptions _samlRoutes = samlOptions.Value.UserInteraction;
    private readonly UserInteractionOptions _identityServerRoutes = identityServerOptions.Value.UserInteraction;

    internal Uri SamlConsentUri()
    {
        var consentUrl = _identityServerRoutes.ConsentUrl
                           ?? throw new InvalidOperationException("No consent url configured");

        var returnUrlParameter = _identityServerRoutes.ConsentReturnUrlParameter
                                 ?? throw new InvalidOperationException("No Consent return url configured");


        return BuildRedirectUrl(consentUrl, returnUrlParameter);
    }

    internal Uri SamlLoginUri()
    {
        var loginPageUrl = _identityServerRoutes.LoginUrl
                           ?? throw new InvalidOperationException("No login url configured");
        var returnUrlParameter = _identityServerRoutes.LoginReturnUrlParameter
                                 ?? throw new InvalidOperationException("No Login return url configured");


        return BuildRedirectUrl(loginPageUrl, returnUrlParameter);
    }

    internal Uri SamlLogoutUri(string logoutId)
    {
        var logoutPageUrl = _identityServerRoutes.LogoutUrl ?? throw new InvalidOperationException("No logout url configured");
        var logoutIdParameter = _identityServerRoutes.LogoutIdParameter ?? throw new InvalidOperationException("No logout id parameter configured");

        logoutPageUrl = logoutPageUrl.AddQueryString(logoutIdParameter, logoutId);

        return new Uri(logoutPageUrl, logoutPageUrl.IsLocalUrl() ? UriKind.Relative : UriKind.Absolute);
    }

    internal Uri SamlSignInCallBackUri()
    {
        var signInCallBackUrl = _samlRoutes.Route + _samlRoutes.SignInCallbackPath;

        return new Uri(signInCallBackUrl, UriKind.Relative);
    }

    internal Uri SamlLogoutCallBackUri()
    {
        var logoutCallbackUri = _samlRoutes.Route + _samlRoutes.SingleLogoutCallbackPath;

        return new Uri(logoutCallbackUri, UriKind.Relative);
    }

    private Uri BuildRedirectUrl(string redirectUrl, string returnUrlParameter)
    {
        var returnUrl = BuildReturnUrl();

        var uriKind = UriKind.Relative;
        if (!redirectUrl.IsLocalUrl())
        {
            // The login page is hosted externally. So, the return url needs to be absolute.
            // Since the return url is hosted by us, we can make absolute from the server url.
            returnUrl = urls.GetAbsoluteUrl(returnUrl);
            uriKind = UriKind.Absolute;
        }

        var queryString = new QueryString();
        queryString = queryString.Add(returnUrlParameter, returnUrl);

        return new Uri(redirectUrl + queryString, uriKind);
    }

    private string BuildReturnUrl() => _samlRoutes.Route + _samlRoutes.SignInCallbackPath;
}
