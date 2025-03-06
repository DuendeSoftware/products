// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for denied consent.
/// </summary>
/// <seealso cref="Event" />
public class ConsentDeniedEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentDeniedEvent" /> class.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="requestedScopes">The requested scopes.</param>
    public ConsentDeniedEvent(string subjectId, string clientId, IEnumerable<string> requestedScopes)
        : base(EventCategories.Grants,
            "Consent denied",
            EventTypes.Information,
            EventIds.ConsentDenied)
    {
        SubjectId = subjectId;
        ClientId = clientId;
        RequestedScopes = requestedScopes;
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
}