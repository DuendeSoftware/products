// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
#pragma warning disable CS0618 // Type or member is obsolete
        builder.AddTransforms<AccessTokenTransformProvider>();
#pragma warning restore CS0618 // Type or member is obsolete

        return builder;
    }
}
