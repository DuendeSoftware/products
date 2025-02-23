// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Token request errors
/// </summary>
public enum TokenRequestErrors
{
    /// <summary>
    /// invalid_request
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// invalid_client
    /// </summary>
    InvalidClient,

    /// <summary>
    /// invalid_grant
    /// </summary>
    InvalidGrant,

    /// <summary>
    /// unauthorized_client
    /// </summary>
    UnauthorizedClient,

    /// <summary>
    /// unsupported_grant_type
    /// </summary>
    UnsupportedGrantType,

    /// <summary>
    /// invalid_scope
    /// </summary>
    InvalidScope,

    /// <summary>
    /// invalid_target
    /// </summary>
    InvalidTarget
}
