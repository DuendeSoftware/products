// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Yarp;

/// <summary>
/// YARP related DI extension methods
/// </summary>
public static class BffBuilderExtensions
{
    /// <summary>
    /// Adds the services required for the YARP HTTP forwarder
    /// </summary>
    /// <param name="builder"></param>
    public static BffBuilder AddRemoteApis(this BffBuilder builder)
    {
        builder.Services.AddHttpForwarder();
        return builder;
    }

}
