// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Specialized;

namespace Duende.IdentityServer.Services;

/// <summary>
/// A pushed authorization request that is not serialized.
/// </summary>
public class DeserializedPushedAuthorizationRequest
{
    /// <summary>
    /// The reference value of the pushed authorization request. This is the
    /// identifier within the request_uri.
    /// </summary>
    public required string ReferenceValue { get; set; }

    /// <summary>
    /// The pushed parameters. 
    /// </summary>
    public required NameValueCollection PushedParameters { get; set; }

    /// <summary>
    /// The expiration time.
    /// </summary>
    public required DateTime ExpiresAtUtc { get; set; }
}
