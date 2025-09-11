// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.AccessTokenManagement;

/// <summary>
/// Encapsulates contextual data used to retreive an access token.
/// </summary>
public sealed record AccessTokenRetrievalContext
{
    /// <summary>
    /// The HttpContext of the incoming HTTP request that will be forwarded to
    /// the remote API.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>
    /// Metadata that describes the remote API.
    /// </summary>
    public required BffRemoteApiEndpointMetadata Metadata { get; init; }

    /// <summary>
    /// Additional optional per request parameters for a user access token request.
    /// </summary>
    public required BffUserAccessTokenParameters? UserTokenRequestParameters { get; init; }


    /// <summary>
    /// The locally requested path.
    /// </summary>
    public required PathString PathMatch { get; init; }

    /// <summary>
    /// The remote address of the API.
    /// </summary>
    public required Uri ApiAddress { get; init; }
}
