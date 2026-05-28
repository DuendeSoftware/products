// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for failed SAML SSO.
/// </summary>
/// <seealso cref="Event" />
public class SamlSsoFailureEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamlSsoFailureEvent"/> class.
    /// </summary>
    /// <param name="spEntityId">The service provider entity ID (if known).</param>
    /// <param name="error">The error description.</param>
    /// <param name="endpoint">The endpoint where the failure occurred.</param>
    public SamlSsoFailureEvent(string? spEntityId, string error, string endpoint)
        : base(EventCategories.Saml,
            "SAML SSO Failure",
            EventTypes.Failure,
            EventIds.SamlSsoFailure)
    {
        SpEntityId = spEntityId;
        Error = error;
        Endpoint = endpoint;
    }

    /// <summary>
    /// Gets or sets the service provider entity ID (if known).
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the endpoint where the failure occurred.
    /// </summary>
    public string Endpoint { get; set; }
}
