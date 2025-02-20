// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.Bff;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extensions for YARP transforms
/// </summary>
public static class YarpTransformExtensions
{
    /// <summary>
    /// Adds the transform which will request an access token for the proxied request. 
    /// </summary>
    public static TransformBuilderContext AddBffAccessToken(this TransformBuilderContext context, PathString localPath)
    {
        var proofService = context.Services.GetRequiredService<IDPoPProofService>();
        var logger = context.Services.GetRequiredService<ILogger<AccessTokenRequestTransform>>();
        var options = context.Services.GetRequiredService<IOptions<BffOptions>>();
        context.RequestTransforms.Add(
            new AccessTokenRequestTransform(
                options,
                proofService,
                logger
            ));
        return context;
    }
}