// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models a validated request to the authorize endpoint.
/// </summary>
public class ValidatedAuthorizeRequest : ValidatedRequest
{
    /// <summary>
    /// Gets or sets the type of the response.
    /// </summary>
    /// <value>
    /// The type of the response.
    /// </value>
    public string ResponseType { get; set; } = default!;

    /// <summary>
    /// Gets or sets the response mode.
    /// </summary>
    /// <value>
    /// The response mode.
    /// </value>
    public string ResponseMode { get; set; } = default!;

    /// <summary>
    /// Gets or sets the grant type.
    /// </summary>
    /// <value>
    /// The grant type.
    /// </value>
    public string GrantType { get; set; } = default!;

    /// <summary>
    /// Gets or sets the redirect URI.
    /// </summary>
    /// <value>
    /// The redirect URI.
    /// </value>
    public string RedirectUri { get; set; } = default!;

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    // todo: consider replacing with extension method to access Raw collection; would need to be done wholesale for all props.
    public List<string> RequestedScopes { get; set; } = default!;

    /// <summary>
    /// Gets or sets the requested resource indicators.
    /// </summary>
    public IEnumerable<string>? RequestedResourceIndicators { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether consent was shown.
    /// </summary>
    /// <value>
    ///   <c>true</c> if consent was shown; otherwise, <c>false</c>.
    /// </value>
    public bool WasConsentShown { get; set; }

    /// <summary>
    /// Gets the description the user assigned to the device being authorized.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>
    /// The state.
    /// </value>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the UI locales.
    /// </summary>
    /// <value>
    /// The UI locales.
    /// </value>
    public string? UiLocales { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the request was an OpenID Connect request.
    /// </summary>
    /// <value>
    /// <c>true</c> if the request was an OpenID Connect request; otherwise, <c>false</c>.
    /// </value>
    public bool IsOpenIdRequest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is API resource request.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is API resource request; otherwise, <c>false</c>.
    /// </value>
    public bool IsApiResourceRequest { get; set; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    /// <value>
    /// The nonce.
    /// </value>
    public string? Nonce { get; set; }

    /// <summary>
    /// Gets or sets the authentication context reference classes.
    /// </summary>
    /// <value>
    /// The authentication context reference classes.
    /// </value>
    public List<string>? AuthenticationContextReferenceClasses { get; set; }

    /// <summary>
    /// Gets or sets the display mode.
    /// </summary>
    /// <value>
    /// The display mode.
    /// </value>
    public string? DisplayMode { get; set; }

    /// <summary>
    /// Gets or sets the collection of prompt modes.
    /// </summary>
    /// <remarks>
    /// The <see cref="PromptModes"/> change as they are used. For example, if
    /// the prompt mode is login (to force the login UI to be displayed), the
    /// collection will initially contain login, but when the login page is
    /// displayed, the login prompt will be removed from the collection of
    /// prompt modes so that the login page will only be displayed once.
    /// <para>
    /// See also: <see cref="ProcessedPromptModes"/> and <see
    /// cref="OriginalPromptModes"/>.
    /// </para>
    /// </remarks>
    /// <value>
    /// The collection of prompt modes, which changes as the request is
    /// processed and various prompts are displayed.
    /// </value>
    public IEnumerable<string> PromptModes { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of original prompt modes.
    /// </summary>
    /// <remarks>
    /// The <see cref="PromptModes"/> change as they are used. For example, if
    /// the prompt mode is login (to force the login UI to be displayed), the
    /// collection will initially contain login, but when the login page is
    /// displayed, the login prompt will be removed from the collection of
    /// prompt modes so that the login page will only be displayed once.
    /// <para>
    /// See also:
    /// <list type="bullet">
    /// <item><seealso cref="ProcessedPromptModes"/></item>
    /// <item><seealso cref="PromptModes"/></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <value>
    /// The collection of original prompt modes.
    /// </value>
    public IEnumerable<string> OriginalPromptModes { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of previously processed prompt modes.
    /// </summary>
    /// <remarks>
    /// The <see cref="PromptModes"/> change as they are used. For example, if
    /// the prompt mode is login (to force the login UI to be displayed), the
    /// collection will initially contain login, but when the login page is
    /// displayed, the login prompt will be removed from the collection of
    /// prompt modes so that the login page will only be displayed once.
    /// </remarks>
    /// <para>
    /// See also:
    /// <list type="bullet">
    /// <item><seealso cref="PromptModes"/></item>
    /// <item><seealso cref="OriginalPromptModes"/></item>
    /// </list>
    /// </para>
    /// <value>
    /// The collection of processed prompt modes.
    /// </value>
    public IEnumerable<string> ProcessedPromptModes { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum age.
    /// </summary>
    /// <value>
    /// The maximum age.
    /// </value>
    public int? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the login hint.
    /// </summary>
    /// <value>
    /// The login hint.
    /// </value>
    public string? LoginHint { get; set; }

    /// <summary>
    /// Gets or sets the code challenge
    /// </summary>
    /// <value>
    /// The code challenge
    /// </value>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// Gets or sets the code challenge method
    /// </summary>
    /// <value>
    /// The code challenge method
    /// </value>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// Gets or sets the validated contents of the request object (if present)
    /// </summary>
    /// <value>
    /// The request object values
    /// </value>
    public IEnumerable<Claim> RequestObjectValues { get; set; } = new List<Claim>();

    /// <summary>
    /// Gets or sets the request object (either passed by value or retrieved by reference)
    /// </summary>
    /// <value>
    /// The request object
    /// </value>
    public string? RequestObject { get; set; }

    /// <summary>
    /// The thumbprint of the associated DPoP proof key, if one was used.
    /// </summary>
    public string? DPoPKeyThumbprint { get; set; }

    /// <summary>
    /// The reference value of the pushed authorization request, if one was used. Pushed authorization requests are
    /// passed by reference using the request_uri parameter, which is in the form
    /// urn:ietf:params:oauth:request_uri:{ReferenceValue}, where ReferenceValue is a random identifier. If a
    /// request_uri in that format is passed, the reference value portion will be extracted and saved here.
    /// </summary>
    public string? PushedAuthorizationReferenceValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating the context in which authorization
    /// validation is occurring (the PAR endpoint or the authorize endpoint with
    /// or without pushed parameters).
    /// </summary>
    public AuthorizeRequestType AuthorizeRequestType { get; set; }

    /// <summary>
    /// Gets a value indicating whether an access token was requested.
    /// </summary>
    /// <value>
    /// <c>true</c> if an access token was requested; otherwise, <c>false</c>.
    /// </value>
    public bool AccessTokenRequested => ResponseType == OidcConstants.ResponseTypes.IdTokenToken ||
                                        ResponseType == OidcConstants.ResponseTypes.Code ||
                                        ResponseType == OidcConstants.ResponseTypes.CodeIdToken ||
                                        ResponseType == OidcConstants.ResponseTypes.CodeToken ||
                                        ResponseType == OidcConstants.ResponseTypes.CodeIdTokenToken;



    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatedAuthorizeRequest"/> class.
    /// </summary>
    public ValidatedAuthorizeRequest()
    {
        RequestedScopes = [];
        AuthenticationContextReferenceClasses = [];
    }
}

/// <summary>
/// Indicates the context in which authorization validation is occurring (is
/// this the authorize endpoint with or without PAR or the PAR endpoint itself?)
/// </summary>
public enum AuthorizeRequestType
{
    /// <summary>
    /// A request to the authorize endpoint without PAR
    /// </summary>
    Authorize,
    /// <summary>
    /// A request to the PAR endpoint
    /// </summary>
    PushedAuthorization,
    /// <summary>
    /// A request to the authorize endpoint with pushed parameters
    /// </summary>
    AuthorizeWithPushedParameters
}
