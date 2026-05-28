// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Models;

/// <summary>
/// Enum to model interaction authorization errors.
/// </summary>
public enum InteractionError
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
