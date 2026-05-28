// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for SAML LogoutRequest validation failure.
/// </summary>
/// <seealso cref="Event" />
public class SamlLogoutRequestValidationFailureEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamlLogoutRequestValidationFailureEvent"/> class.
    /// </summary>
    /// <param name="spEntityId">The service provider entity ID (if resolvable).</param>
    /// <param name="error">The validation error.</param>
    /// <param name="binding">The binding used for the request.</param>
    public SamlLogoutRequestValidationFailureEvent(string? spEntityId, string error, string? binding)
        : base(EventCategories.Saml,
            "SAML LogoutRequest Validation Failure",
            EventTypes.Failure,
            EventIds.SamlLogoutRequestValidationFailure)
    {
        SpEntityId = spEntityId;
        Error = error;
        Binding = binding;
    }

    /// <summary>
    /// Gets or sets the service provider entity ID (if resolvable).
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    /// Gets or sets the validation error.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the binding used for the request.
    /// </summary>
    public string? Binding { get; set; }
}
