// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.ResponseHandling;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for aspects of the user interface.
/// </summary>
public class UserInteractionOptions
{
    /// <summary>
    /// Gets or sets the login URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The login URL.
    /// </value>
    public string? LoginUrl { get; set; } //= Constants.UIConstants.DefaultRoutePaths.Login.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the login return URL parameter.
    /// </summary>
    /// <value>
    /// The login return URL parameter.
    /// </value>
    public string? LoginReturnUrlParameter { get; set; } //= Constants.UIConstants.DefaultRoutePathParams.Login;

    /// <summary>
    /// Gets or sets the logout URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The logout URL.
    /// </value>
    public string? LogoutUrl { get; set; } //= Constants.UIConstants.DefaultRoutePaths.Logout.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the logout identifier parameter.
    /// </summary>
    /// <value>
    /// The logout identifier parameter.
    /// </value>
    public string LogoutIdParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Logout;

    /// <summary>
    /// Gets or sets the consent URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The consent URL.
    /// </value>
    public string ConsentUrl { get; set; } = Constants.UIConstants.DefaultRoutePaths.Consent.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the consent return URL parameter.
    /// </summary>
    /// <value>
    /// The consent return URL parameter.
    /// </value>
    public string ConsentReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Consent;

    /// <summary>
    /// Gets or sets the create account (or register) URL, for use with the OIDC prompt parameter. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The create account URL.
    /// </value>
    public string? CreateAccountUrl { get; set; } // null by default to omit support in discovery

    /// <summary>
    /// Gets or sets the create account (or register) return URL parameter.
    /// </summary>
    /// <value>
    /// The create account return URL parameter.
    /// </value>
    public string CreateAccountReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.CreateAccount;

    /// <summary>
    /// Gets or sets the error URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The error URL.
    /// </value>
    public string ErrorUrl { get; set; } = Constants.UIConstants.DefaultRoutePaths.Error.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the error identifier parameter.
    /// </summary>
    /// <value>
    /// The error identifier parameter.
    /// </value>
    public string ErrorIdParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Error;

    /// <summary>
    /// Gets or sets the custom redirect return URL parameter.
    /// </summary>
    /// <value>
    /// The custom redirect return URL parameter.
    /// </value>
    public string CustomRedirectReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Custom;

    /// <summary>
    /// Gets or sets the cookie message threshold. This limits how many cookies are created, and older ones will be purged.
    /// </summary>
    /// <value>
    /// The cookie message threshold.
    /// </value>
    public int CookieMessageThreshold { get; set; } = Constants.UIConstants.CookieMessageThreshold;

    /// <summary>
    /// Gets or sets the device verification URL.  If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The device verification URL.
    /// </value>
    public string DeviceVerificationUrl { get; set; } = Constants.UIConstants.DefaultRoutePaths.DeviceVerification;

    /// <summary>
    /// Gets or sets the device verification user code parameter.
    /// </summary>
    /// <value>
    /// The device verification user code parameter.
    /// </value>
    public string DeviceVerificationUserCodeParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.UserCode;

    /// <summary>
    /// Flag that allows return URL validation to accept full URL that includes the IdentityServer origin. Defaults to false.
    /// </summary>
    public bool AllowOriginInReturnUrl { get; set; }

    /// <summary>
    /// The collection of OIDC prompt modes supported and that will be published
    /// in discovery. By default, this includes all values in <see
    /// cref="Constants.SupportedPromptModes"/>. If the <see
    /// cref="CreateAccountUrl"/> option is set, then the "create" value is also
    /// included. If additional prompt values are added, a customized <see
    /// cref="IAuthorizeInteractionResponseGenerator"/> is also required to
    /// handle those values.
    /// </summary>
    public ICollection<string> PromptValuesSupported { get; set; } = new HashSet<string>(Constants.SupportedPromptModes);
}
