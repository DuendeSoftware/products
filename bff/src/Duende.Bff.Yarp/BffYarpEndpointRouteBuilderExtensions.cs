// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Duende.Bff.Yarp;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for the BFF endpoints
/// </summary>
public static class BffYarpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Adds a remote BFF API endpoint
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="localPath"></param>
    /// <param name="apiAddress"></param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapRemoteBffApiEndpoint(
        this IEndpointRouteBuilder endpoints,
        PathString localPath, 
        string apiAddress)
    {
        endpoints.CheckLicense();
            
        return endpoints.Map(
                localPath.Add("/{**catch-all}").Value!,
                RemoteApiEndpoint.Map(localPath, apiAddress))
            .WithMetadata(new BffRemoteApiEndpointMetadata());
    }
}