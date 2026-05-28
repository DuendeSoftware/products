// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.ResponseHandling;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for user-facing UI pages, including URLs, query parameter names, and other
/// behavior related to interactive authorization flows.
/// </summary>
public class UserInteractionOptions
{
    /// <summary>
    /// Gets or sets the URL of the login page. Local URLs must begin with a leading slash.
    /// </summary>
    /// <remarks>
    /// When not set, IdentityServer uses the default route configured by the UI template.
    /// </remarks>
    public string? LoginUrl { get; set; } //= Constants.UIConstants.DefaultRoutePaths.Login.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the name of the query parameter appended to the login URL that carries the return URL
    /// after successful authentication.
    /// </summary>
    /// <remarks>Defaults to <c>"returnUrl"</c>.</remarks>
    public string? LoginReturnUrlParameter { get; set; } //= Constants.UIConstants.DefaultRoutePathParams.Login;

    /// <summary>
    /// Gets or sets the URL of the logout page. Local URLs must begin with a leading slash.
    /// </summary>
    /// <remarks>
    /// When not set, IdentityServer uses the default route configured by the UI template.
    /// </remarks>
    public string? LogoutUrl { get; set; } //= Constants.UIConstants.DefaultRoutePaths.Logout.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the name of the query parameter appended to the logout URL that carries the logout
    /// message identifier.
    /// </summary>
    /// <remarks>Defaults to <c>"logoutId"</c>.</remarks>
    public string LogoutIdParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Logout;

    /// <summary>
    /// Gets or sets the URL of the consent page. Local URLs must begin with a leading slash.
    /// </summary>
    /// <remarks>Defaults to <c>"/consent"</c>.</remarks>
    public string ConsentUrl { get; set; } = Constants.UIConstants.DefaultRoutePaths.Consent.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the name of the query parameter appended to the consent URL that carries the return URL
    /// after the user grants or denies consent.
    /// </summary>
    /// <remarks>Defaults to <c>"returnUrl"</c>.</remarks>
    public string ConsentReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Consent;

    /// <summary>
    /// Gets or sets the URL of the account creation (registration) page, used when an authorization request
    /// includes <c>prompt=create</c>. Local URLs must begin with a leading slash.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>null</c>. When set, the <c>prompt=create</c> parameter redirects users
    /// to this URL, and <c>"create"</c> is added to the <c>prompt_values_supported</c> array
    /// in the discovery document. When not set, <c>prompt=create</c> is ignored.
    /// </remarks>
    public string? CreateAccountUrl { get; set; } // null by default to omit support in discovery

    /// <summary>
    /// Gets or sets the name of the query parameter appended to the create-account URL that carries the
    /// return URL after account creation.
    /// </summary>
    /// <remarks>Defaults to <c>"returnUrl"</c>.</remarks>
    public string CreateAccountReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.CreateAccount;

    /// <summary>
    /// Gets or sets the URL of the error page. Local URLs must begin with a leading slash.
    /// </summary>
    /// <remarks>Defaults to <c>"/error"</c>.</remarks>
    public string ErrorUrl { get; set; } = Constants.UIConstants.DefaultRoutePaths.Error.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the name of the query parameter appended to the error URL that carries the error message
    /// identifier.
    /// </summary>
    /// <remarks>Defaults to <c>"errorId"</c>.</remarks>
    public string ErrorIdParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Error;

    /// <summary>
    /// Gets or sets the name of the query parameter appended to a custom redirect URL from the authorization
    /// endpoint that carries the return URL.
    /// </summary>
    /// <remarks>Defaults to <c>"returnUrl"</c>.</remarks>
    public string CustomRedirectReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Custom;

    /// <summary>
    /// Gets or sets the maximum number of message cookies of any type that IdentityServer will create.
    /// Older cookies are purged once this limit is reached.
    /// </summary>
    /// <remarks>
    /// Defaults to 2. This limit exists because browsers cap the total number and size of
    /// cookies per domain. In practice, this value controls how many concurrent browser tabs a
    /// user can have open while interacting with IdentityServer.
    /// </remarks>
    public int CookieMessageThreshold { get; set; } = Constants.UIConstants.CookieMessageThreshold;

    /// <summary>
    /// Gets or sets the URL of the device verification page used in the OAuth 2.0 Device Authorization
    /// Grant. Local URLs must begin with a leading slash.
    /// </summary>
    /// <remarks>Defaults to <c>"/device"</c>.</remarks>
    public string DeviceVerificationUrl { get; set; } = Constants.UIConstants.DefaultRoutePaths.DeviceVerification;

    /// <summary>
    /// Gets or sets the name of the query parameter appended to the device verification URL that carries
    /// the user code.
    /// </summary>
    /// <remarks>Defaults to <c>"userCode"</c>.</remarks>
    public string DeviceVerificationUserCodeParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.UserCode;

    /// <summary>
    /// Gets or sets a value indicating whether return URL validation accepts absolute URLs that include the IdentityServer
    /// origin.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When <c>true</c>, return URLs such as
    /// <c>https://identity.example.com/connect/authorize/callback</c> are accepted in addition
    /// to relative paths.
    /// </remarks>
    public bool AllowOriginInReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the collection of OIDC <c>prompt</c> parameter values that IdentityServer supports and
    /// publishes in the <c>prompt_values_supported</c> discovery document array.
    /// </summary>
    /// <remarks>
    /// By default, this includes all values in <see cref="Constants.SupportedPromptModes"/>.
    /// When <see cref="CreateAccountUrl"/> is set, <c>"create"</c> is also included
    /// automatically. Adding custom prompt values requires a corresponding customization of
    /// <see cref="IAuthorizeInteractionResponseGenerator"/> to handle those values.
    /// </remarks>
    public ICollection<string> PromptValuesSupported { get; set; } = new HashSet<string>(Constants.SupportedPromptModes);
}
