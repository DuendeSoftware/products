// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for failed SAML Single Logout.
/// </summary>
/// <seealso cref="Event" />
public class SamlSloFailureEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamlSloFailureEvent"/> class.
    /// </summary>
    /// <param name="spEntityId">The service provider entity ID (if known).</param>
    /// <param name="error">The error description.</param>
    public SamlSloFailureEvent(string? spEntityId, string error)
        : base(EventCategories.Saml,
            "SAML SLO Failure",
            EventTypes.Failure,
            EventIds.SamlSloFailure)
    {
        SpEntityId = spEntityId;
        Error = error;
    }

    /// <summary>
    /// Gets or sets the service provider entity ID (if known).
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    public string Error { get; set; }
}
