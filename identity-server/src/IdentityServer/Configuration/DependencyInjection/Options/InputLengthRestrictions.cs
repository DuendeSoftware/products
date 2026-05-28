// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Maximum allowed lengths for protocol request parameters. Requests that exceed these limits
/// are rejected with a validation error.
/// </summary>
public class InputLengthRestrictions
{
    private const int Default = 100;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>client_id</c> parameter. Defaults to 100.
    /// </summary>
    public int ClientId { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for external client secrets. Defaults to 100.
    /// </summary>
    public int ClientSecret { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>scope</c> parameter. Defaults to 300.
    /// </summary>
    public int Scope { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>redirect_uri</c> parameter. Defaults to 400.
    /// </summary>
    public int RedirectUri { get; set; } = 400;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>nonce</c> parameter. Defaults to 300.
    /// </summary>
    public int Nonce { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>ui_locale</c> parameter. Defaults to 100.
    /// </summary>
    public int UiLocale { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>login_hint</c> parameter. Defaults to 100.
    /// </summary>
    public int LoginHint { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>acr_values</c> parameter. Defaults to 300.
    /// </summary>
    public int AcrValues { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>grant_type</c> parameter. Defaults to 100.
    /// </summary>
    public int GrantType { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>username</c> parameter. Defaults to 100.
    /// </summary>
    public int UserName { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>password</c> parameter. Defaults to 100.
    /// </summary>
    public int Password { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for Content Security Policy report bodies. Defaults to 2000.
    /// </summary>
    public int CspReport { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the maximum allowed length for external identity provider names. Defaults to 100.
    /// </summary>
    public int IdentityProvider { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for error messages returned by external identity providers.
    /// Defaults to 100.
    /// </summary>
    public int ExternalError { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for authorization codes. Defaults to 100.
    /// </summary>
    public int AuthorizationCode { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for device codes used in the device authorization flow.
    /// Defaults to 100.
    /// </summary>
    public int DeviceCode { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for refresh token handles. Defaults to 100.
    /// </summary>
    public int RefreshToken { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for reference token handles. Defaults to 100.
    /// </summary>
    public int TokenHandle { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for JWT strings (e.g., request objects, client assertions).
    /// Defaults to 51200 (50 KB).
    /// </summary>
    public int Jwt { get; set; } = 51200;

    /// <summary>
    /// Gets the minimum required length for the PKCE <c>code_challenge</c> parameter. Defaults to 43.
    /// </summary>
    /// <remarks>
    /// The minimum of 43 characters is specified by
    /// <see href="https://datatracker.ietf.org/doc/html/rfc7636">RFC 7636</see>.
    /// </remarks>
    public int CodeChallengeMinLength { get; } = 43;

    /// <summary>
    /// Gets the maximum allowed length for the PKCE <c>code_challenge</c> parameter. Defaults to 128.
    /// </summary>
    /// <remarks>
    /// The maximum of 128 characters is specified by
    /// <see href="https://datatracker.ietf.org/doc/html/rfc7636">RFC 7636</see>.
    /// </remarks>
    public int CodeChallengeMaxLength { get; } = 128;

    /// <summary>
    /// Gets the minimum required length for the PKCE <c>code_verifier</c> parameter. Defaults to 43.
    /// </summary>
    /// <remarks>
    /// The minimum of 43 characters is specified by
    /// <see href="https://datatracker.ietf.org/doc/html/rfc7636">RFC 7636</see>.
    /// </remarks>
    public int CodeVerifierMinLength { get; } = 43;

    /// <summary>
    /// Gets the maximum allowed length for the PKCE <c>code_verifier</c> parameter. Defaults to 128.
    /// </summary>
    /// <remarks>
    /// The maximum of 128 characters is specified by
    /// <see href="https://datatracker.ietf.org/doc/html/rfc7636">RFC 7636</see>.
    /// </remarks>
    public int CodeVerifierMaxLength { get; } = 128;

    /// <summary>
    /// Gets the maximum allowed length for the <c>resource</c> indicator parameter. Defaults to 512.
    /// </summary>
    public int ResourceIndicatorMaxLength { get; } = 512;

    /// <summary>
    /// Gets or sets the maximum allowed length for the CIBA <c>binding_message</c> parameter. Defaults to 100.
    /// </summary>
    public int BindingMessage { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the device flow <c>user_code</c> parameter. Defaults to 100.
    /// </summary>
    public int UserCode { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the <c>id_token_hint</c> parameter. Defaults to 4000.
    /// </summary>
    public int IdTokenHint { get; set; } = 4000;

    /// <summary>
    /// Gets or sets the maximum allowed length for the CIBA <c>login_hint_token</c> parameter. Defaults to 4000.
    /// </summary>
    public int LoginHintToken { get; set; } = 4000;

    /// <summary>
    /// Gets or sets the maximum allowed length for the CIBA <c>auth_req_id</c> parameter. Defaults to 100.
    /// </summary>
    public int AuthenticationRequestId { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for the DPoP <c>dpop_jkt</c> (key thumbprint) parameter.
    /// Defaults to 100.
    /// </summary>
    public int DPoPKeyThumbprint { get; set; } = Default;

    /// <summary>
    /// Gets or sets the maximum allowed length for DPoP proof token strings. Defaults to 4000.
    /// </summary>
    public int DPoPProofToken { get; set; } = 4000;
}
