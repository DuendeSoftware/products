// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp.Internal;

namespace Duende.Bff.Yarp;

public static class ProxyFrontendExtensionExtensions
{
    public static BffFrontend WithRemoteApis(this BffFrontend frontend, params RemoteApi[] config)
    {
        // Remove existing ProxyFrontendExtension if present
        var newExtensions = frontend.DataExtensions
            .Where(e => e is not ProxyBffDataExtension)
            // Add new ProxyFrontendExtension with replaced routes
            .Append(new ProxyBffDataExtension { RemoteApis = config.ToArray() })
            .ToArray();

        // Clone frontend with new extensions
        return frontend with
        {
            DataExtensions = newExtensions
        };
    }

    /// <summary>
    /// This will become an extension property as of c# 14
    /// </summary>
    /// <param name="frontend"></param>
    /// <returns></returns>
    public static RemoteApi[] GetRemoteApis(this BffFrontend frontend) => frontend.DataExtensions
        .OfType<ProxyBffDataExtension>()
        .SelectMany(e => e.RemoteApis)
        .ToArray();
}
