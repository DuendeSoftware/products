// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Base class for responses from the pushed authorization endpoint.
/// </summary>
public abstract class PushedAuthorizationResponse
{ }

/// <summary>
/// Represents failure from the pushed authorization endpoint.
/// </summary>
public class PushedAuthorizationFailure : PushedAuthorizationResponse
{
    /// <summary>
    /// The error code, as specified by RFC 9126, etc.
    /// </summary>
    public required string Error { get; set; }

    /// <summary>
    /// The error description: "human-readable ASCII text providing
    /// additional information, used to assist the client developer in
    /// understanding the error that occurred."
    /// </summary>
    public required string ErrorDescription { get; set; }

    /// <summary>
    /// The DPoP nonce header to emit, or null if no nonce is to be emitted.
    /// </summary>
    public string DPoPNonce { get; set; }
}

/// <summary>
/// Represents success from the pushed authorization endpoint.
/// </summary>
public class PushedAuthorizationSuccess : PushedAuthorizationResponse
{
    /// <summary>
    /// The request uri for the pushed request, in the format urn:ietf:params:oauth:request_uri:{ReferenceValue}.
    /// </summary>
    public required string RequestUri { get; set; }
    /// <summary>
    /// The number of seconds from now that the pushed request will expire.
    /// </summary>
    public required int ExpiresIn { get; set; }
}
