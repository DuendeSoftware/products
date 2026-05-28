// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for successful SAML Single Logout.
/// </summary>
/// <seealso cref="Event" />
public class SamlSloSuccessEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamlSloSuccessEvent"/> class.
    /// </summary>
    /// <param name="spEntityId">The service provider entity ID.</param>
    /// <param name="sessionIndex">The SAML session index.</param>
    /// <param name="initiator">Who initiated the logout (SP or IdP).</param>
    public SamlSloSuccessEvent(string spEntityId, string? sessionIndex, string initiator)
        : base(EventCategories.Saml,
            "SAML SLO Success",
            EventTypes.Success,
            EventIds.SamlSloSuccess)
    {
        SpEntityId = spEntityId;
        SessionIndex = sessionIndex;
        Initiator = initiator;
    }

    /// <summary>
    /// Gets or sets the service provider entity ID.
    /// </summary>
    public string SpEntityId { get; set; }

    /// <summary>
    /// Gets or sets the SAML session index.
    /// </summary>
    public string? SessionIndex { get; set; }

    /// <summary>
    /// Gets or sets who initiated the logout (SP or IdP).
    /// </summary>
    public string Initiator { get; set; }
}
