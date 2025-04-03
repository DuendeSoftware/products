// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Http;

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
/// Encapsulates contextual data used to retreive an access token.
/// </summary>
public class AccessTokenRetrievalContext
{
    /// <summary>
    /// The HttpContext of the incoming HTTP request that will be forwarded to
    /// the remote API.
    /// </summary>
    public required HttpContext HttpContext { get; set; }

    /// <summary>
    /// Metadata that describes the remote API.
    /// </summary>
    public required BffRemoteApiEndpointMetadata Metadata { get; set; }

    /// <summary>
    /// Additional optional per request parameters for a user access token request.
    /// </summary>
    public required UserTokenRequestParameters? UserTokenRequestParameters { get; set; }


    /// <summary>
    /// The locally requested path.
    /// </summary>
    public required PathString LocalPath { get; set; }

    /// <summary>
    /// The remote address of the API.
    /// </summary>
    public required Uri ApiAddress { get; set; }
}
