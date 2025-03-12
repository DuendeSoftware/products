// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for granted consent.
/// </summary>
/// <seealso cref="Event" />
public class ConsentGrantedEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentGrantedEvent" /> class.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="requestedScopes">The requested scopes.</param>
    /// <param name="grantedScopes">The granted scopes.</param>
    /// <param name="consentRemembered">if set to <c>true</c> consent was remembered.</param>
    public ConsentGrantedEvent(string subjectId, string clientId, IEnumerable<string> requestedScopes, IEnumerable<string> grantedScopes, bool consentRemembered)
        : base(EventCategories.Grants,
            "Consent granted",
            EventTypes.Information,
            EventIds.ConsentGranted)
    {
        SubjectId = subjectId;
        ClientId = clientId;
        RequestedScopes = requestedScopes;
        GrantedScopes = grantedScopes;
        ConsentRemembered = consentRemembered;
    }

    /// <summary>
    /// Gets or sets the subject identifier.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    /// <value>
    /// The client identifier.
    /// </value>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public IEnumerable<string> RequestedScopes { get; set; }

    /// <summary>
    /// Gets or sets the granted scopes.
    /// </summary>
    /// <value>
    /// The granted scopes.
    /// </value>
    public IEnumerable<string> GrantedScopes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether consent was remembered.
    /// </summary>
    /// <value>
    ///   <c>true</c> if consent was remembered; otherwise, <c>false</c>.
    /// </value>
    public bool ConsentRemembered { get; set; }
}
