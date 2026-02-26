// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents the decision rendered by the SAML authority
/// </summary>
internal enum DecisionType
{
    /// <summary>
    /// The specified action is permitted
    /// </summary>
    Permit,

    /// <summary>
    /// The specified action is denied
    /// </summary>
    Deny,

    /// <summary>
    /// The SAML authority cannot determine whether the action is permitted or denied
    /// </summary>
    Indeterminate
}
