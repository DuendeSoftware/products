// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the validated singout context.
/// </summary>
public class LogoutMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutMessage"/> class.
    /// </summary>
    public LogoutMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutMessage"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    public LogoutMessage(ValidatedEndSessionRequest request)
    {
        if (request != null)
        {
            if (request.Raw != null)
            {
                Parameters = request.Raw.ToFullDictionary();
            }

            // optimize params sent to logout page, since we'd like to send them in URL (not as cookie)
            Parameters.Remove(OidcConstants.EndSessionRequest.IdTokenHint);
            Parameters.Remove(OidcConstants.EndSessionRequest.PostLogoutRedirectUri);
            Parameters.Remove(OidcConstants.EndSessionRequest.State);
            Parameters.Remove(OidcConstants.AuthorizeRequest.UiLocales);

            ClientId = request.Client?.ClientId;
            ClientName = request.Client?.ClientName;
            SubjectId = request.Subject?.GetSubjectId();
            SessionId = request.SessionId;
            ClientIds = request.ClientIds;
            SamlSessions = request.SamlSessions;
            UiLocales = request.UiLocales;

            if (request.PostLogOutUri != null)
            {
                PostLogoutRedirectUri = request.PostLogOutUri;
                if (request.State != null)
                {
                    PostLogoutRedirectUri = PostLogoutRedirectUri.AddQueryString(OidcConstants.EndSessionRequest.State, request.State);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client name.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Gets or sets the post logout redirect URI.
    /// </summary>
    public string? PostLogoutRedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the subject identifier for the user at logout time.
    /// </summary>
    public string? SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the session identifier for the user at logout time.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    ///  Ids of clients known to have an authentication session for user at end session time
    /// </summary>
    public IEnumerable<string>? ClientIds { get; set; }

    /// <summary>
    /// Gets or sets the EntityId of the SAML Service Provider that initiated logout.
    /// Null if this is not a SAML-initiated logout.
    /// </summary>
    public string? SamlServiceProviderEntityId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the SAML LogoutRequest being responded to.
    /// Null if this is not a SAML-initiated logout.
    /// </summary>
    public string? SamlLogoutRequestId { get; set; }

    /// <summary>
    /// Gets or sets the SAML RelayState parameter to return to the SP.
    /// Null if this is not a SAML-initiated logout or no RelayState was provided.
    /// </summary>
    public string? SamlRelayState { get; set; }

    /// <summary>
    /// SAML Service Provider sessions for the user at logout time.
    /// Contains full session data required for logout notifications.
    /// </summary>
    public IEnumerable<SamlSpSessionData>? SamlSessions { get; set; }

    /// <summary>
    /// The UI locales.
    /// </summary>
    public string? UiLocales { get; set; }

    /// <summary>
    /// Gets the entire parameter collection.
    /// </summary>
    public IDictionary<string, string[]> Parameters { get; set; } = new Dictionary<string, string[]>();

    /// <summary>
    ///  Flag to indicate if the payload contains useful information or not to avoid serialization.
    /// </summary>
    internal bool ContainsPayload => ClientId.IsPresent()
        || ClientIds?.Any() == true
        || SamlServiceProviderEntityId.IsPresent()
        || SamlSessions?.Any() == true;
}

/// <summary>
/// Models the request from a client to sign the user out.
/// </summary>
public class LogoutRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutRequest"/> class.
    /// </summary>
    /// <param name="iframeUrl">The iframe URL.</param>
    /// <param name="message">The message.</param>
    public LogoutRequest(string iframeUrl, LogoutMessage? message)
    {
        if (message != null)
        {
            ClientId = message.ClientId;
            ClientName = message.ClientName;
            PostLogoutRedirectUri = message.PostLogoutRedirectUri;
            SubjectId = message.SubjectId;
            SessionId = message.SessionId;
            ClientIds = message.ClientIds;
            UiLocales = message.UiLocales;
            SamlServiceProviderEntityId = message.SamlServiceProviderEntityId;
            SamlLogoutRequestId = message.SamlLogoutRequestId;
            SamlRelayState = message.SamlRelayState;
            SamlSessions = message.SamlSessions;
            Parameters = message.Parameters.FromFullDictionary();
        }

        SignOutIFrameUrl = iframeUrl;
    }

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client name.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Gets or sets the post logout redirect URI.
    /// </summary>
    public string? PostLogoutRedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the subject identifier for the user at logout time.
    /// </summary>
    public string? SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the session identifier for the user at logout time.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    ///  Ids of clients known to have an authentication session for user at end session time
    /// </summary>
    public IEnumerable<string>? ClientIds { get; set; }

    /// <summary>
    /// Gets or sets the EntityId of the SAML Service Provider that initiated logout.
    /// Null if this is not a SAML-initiated logout.
    /// </summary>
    public string? SamlServiceProviderEntityId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the SAML LogoutRequest being responded to.
    /// Null if this is not a SAML-initiated logout.
    /// </summary>
    public string? SamlLogoutRequestId { get; set; }

    /// <summary>
    /// Gets or sets the SAML RelayState parameter to return to the SP.
    /// Null if this is not a SAML-initiated logout or no RelayState was provided.
    /// </summary>
    public string? SamlRelayState { get; set; }

    /// <summary>
    /// SAML Service Provider sessions for the user at logout time.
    /// Contains full session data required for logout notifications.
    /// </summary>
    public IEnumerable<SamlSpSessionData>? SamlSessions { get; set; }

    /// <summary>
    /// The UI locales.
    /// </summary>
    public string? UiLocales { get; set; }

    /// <summary>
    /// Gets the entire parameter collection.
    /// </summary>
    public NameValueCollection Parameters { get; } = new NameValueCollection();

    /// <summary>
    /// Gets or sets the sign out iframe URL.
    /// </summary>
    /// <value>
    /// The sign out iframe URL.
    /// </value>
    public string? SignOutIFrameUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user should be prompted for signout.
    /// </summary>
    /// <value>
    ///   <c>true</c> if the signout prompt should be shown; otherwise, <c>false</c>.
    /// </value>
    public bool ShowSignoutPrompt => ClientId.IsMissing();
}
