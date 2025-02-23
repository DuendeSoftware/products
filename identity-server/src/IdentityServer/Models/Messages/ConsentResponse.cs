// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the user's response to the consent screen.
/// </summary>
public class ConsentResponse
{
    /// <summary>
    /// Error, if any, for the consent response.
    /// </summary>
    public AuthorizationError? Error { get; set; }

    /// <summary>
    /// Error description.
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// Gets if consent was granted.
    /// </summary>
    /// <value>
    ///   <c>true</c> if consent was granted; otherwise, <c>false</c>.
    /// </value>
    public bool Granted => ScopesValuesConsented != null && ScopesValuesConsented.Any() && Error == null;

    /// <summary>
    /// Gets or sets the scope values consented to.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public IEnumerable<string>? ScopesValuesConsented { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user wishes the consent to be remembered.
    /// </summary>
    /// <value>
    ///   <c>true</c> if consent is to be remembered; otherwise, <c>false</c>.
    /// </value>
    public bool RememberConsent { get; set; }

    /// <summary>
    /// Gets the description of the device.
    /// </summary>
    /// <value>
    /// The description of the device.
    /// </value>
    public string? Description { get; set; }
}

/// <summary>
/// Enum to model interaction authorization errors.
/// </summary>
public enum AuthorizationError
{
    /// <summary>
    /// Access denied
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Interaction required
    /// </summary>
    InteractionRequired,

    /// <summary>
    /// Login required
    /// </summary>
    LoginRequired,

    /// <summary>
    /// Account selection required
    /// </summary>
    AccountSelectionRequired,

    /// <summary>
    /// Consent required
    /// </summary>
    ConsentRequired,

    /// <summary>
    /// Temporarily unavailable
    /// </summary>
    TemporarilyUnavailable,

    /// <summary>
    /// Unmet Authentication Requirements
    /// </summary>
    UnmetAuthenticationRequirements,
}
