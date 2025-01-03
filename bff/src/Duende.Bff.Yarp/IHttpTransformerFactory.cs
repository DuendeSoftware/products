// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Yarp.ReverseProxy.Forwarder;

namespace Duende.Bff.Yarp;

/// <summary>
/// Factory for creating a HTTP transformer for outgoing remote BFF API calls
/// </summary>
public interface IHttpTransformerFactory
{
    /// <summary>
    /// Creates a HTTP transformer based on the local path
    /// </summary>
    /// <param name="localPath">Local path the remote API is mapped to</param>
    /// <param name="accessToken">The access token to attach to the request (if present)</param>
    /// <returns></returns>
    HttpTransformer CreateTransformer(string localPath, AccessTokenResult accessToken);
}