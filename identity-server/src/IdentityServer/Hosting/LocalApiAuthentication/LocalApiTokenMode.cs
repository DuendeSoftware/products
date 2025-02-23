// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable


namespace Duende.IdentityServer.Hosting.LocalApiAuthentication;

/// <summary>
/// Models the type of tokens accepted for local API authentication
/// </summary>
public enum LocalApiTokenMode
{
    /// <summary>
    /// Only bearer tokens will be accepted
    /// </summary>
    BearerOnly,
    /// <summary>
    /// Only DPoP tokens will be accepted
    /// </summary>
    DPoPOnly,
    /// <summary>
    /// Both DPoP and Bearer tokens will be accepted
    /// </summary>
    DPoPAndBearer
}
