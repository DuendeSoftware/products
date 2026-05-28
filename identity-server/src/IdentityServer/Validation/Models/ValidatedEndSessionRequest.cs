// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Represents a validated end session (logout) request
/// </summary>
public class ValidatedEndSessionRequest : ValidatedRequest
{
    /// <summary>
    /// Gets a value indicating whether this instance is authenticated.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is authenticated; otherwise, <c>false</c>.
    /// </value>
    public bool IsAuthenticated => Client != null;

    /// <summary>
    /// Gets or sets the post-logout URI.
    /// </summary>
    /// <value>
    /// The post-logout URI.
    /// </value>
    public string PostLogOutUri { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>
    /// The state.
    /// </value>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the UI locales.
    /// </summary>
    /// <value>
    /// The UI locales.
    /// </value>
    public string UiLocales { get; set; }

    /// <summary>
    ///  Ids of clients known to have an authentication session for user at end session time
    /// </summary>
    public IReadOnlyCollection<string> ClientIds { get; set; }

    /// <summary>
    /// SAML Service Provider sessions for the user at end session time.
    /// Contains full session data including EntityIds, NameIds, and SessionIndexes required for logout notifications.
    /// </summary>
    public IReadOnlyCollection<SamlSpSessionData> SamlSessions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the logout UI should prompt the user to confirm
    /// the logout. This is set to <c>true</c> when the id_token_hint validation returns
    /// <see cref="EndSessionHintValidationOutcome.RequiresConfirmation"/>.
    /// </summary>
    /// <remarks>
    /// This flag propagates to <see cref="Duende.IdentityServer.Models.LogoutMessage"/> and then to
    /// <see cref="Duende.IdentityServer.Models.LogoutRequest.ShowSignoutPrompt"/>.
    /// Custom logout UI implementations must respect <see cref="Duende.IdentityServer.Models.LogoutRequest.ShowSignoutPrompt"/>
    /// to enforce the confirmation prompt per OIDC RP-Initiated Logout requirements.
    /// </remarks>
    public bool RequiresConfirmation { get; set; }
}
