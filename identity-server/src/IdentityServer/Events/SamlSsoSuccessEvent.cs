// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for successful SAML SSO assertion issuance.
/// </summary>
/// <seealso cref="Event" />
public class SamlSsoSuccessEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamlSsoSuccessEvent"/> class.
    /// </summary>
    /// <param name="spEntityId">The service provider entity ID.</param>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="sessionIndex">The SAML session index.</param>
    /// <param name="binding">The binding used for the response.</param>
    /// <param name="nameIdFormat">The NameID format used.</param>
    public SamlSsoSuccessEvent(string spEntityId, string? subjectId, string sessionIndex, string binding, string? nameIdFormat)
        : base(EventCategories.Saml,
            "SAML SSO Success",
            EventTypes.Success,
            EventIds.SamlSsoSuccess)
    {
        SpEntityId = spEntityId;
        SubjectId = subjectId;
        SessionIndex = sessionIndex;
        Binding = binding;
        NameIdFormat = nameIdFormat;
    }

    /// <summary>
    /// Gets or sets the service provider entity ID.
    /// </summary>
    public string SpEntityId { get; set; }

    /// <summary>
    /// Gets or sets the subject identifier.
    /// </summary>
    public string? SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the SAML session index.
    /// </summary>
    public string SessionIndex { get; set; }

    /// <summary>
    /// Gets or sets the binding used for the response.
    /// </summary>
    public string Binding { get; set; }

    /// <summary>
    /// Gets or sets the NameID format.
    /// </summary>
    public string? NameIdFormat { get; set; }
}
