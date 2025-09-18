// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Yarp.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Yarp;

/// <summary>
/// Extensions for IReverseProxyBuilder
/// </summary>
public static class ReverseProxyBuilderExtensions
{
    /// <summary>
    /// Wire up BFF YARP extensions to DI
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IReverseProxyBuilder AddBffExtensions(this IReverseProxyBuilder builder)
    {
        builder.AddTransforms<AccessTokenTransformProvider>();

        return builder;
    }
}
