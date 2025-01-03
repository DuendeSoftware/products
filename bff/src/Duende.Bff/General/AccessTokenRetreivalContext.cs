// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Http;

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
    public HttpContext HttpContext { get; set; } = default!;
    
    /// <summary>
    /// Metadata that describes the remote API.
    /// </summary>
    public BffRemoteApiEndpointMetadata Metadata { get; set; } = default!;
    
    /// <summary>
    /// The locally requested path.
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;
    
    /// <summary>
    /// The remote address of the API.
    /// </summary>
    public string ApiAddress { get; set; } = string.Empty;

    /// <summary>
    /// Additional optional per request parameters for a user access token request.
    /// </summary>
    public UserTokenRequestParameters? UserTokenRequestParameters { get; set; }
}
