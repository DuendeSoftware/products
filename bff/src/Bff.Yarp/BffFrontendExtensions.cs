// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;

namespace Duende.Bff.Yarp;

public static class BffFrontendExtensions
{
    public static BffFrontend WithRemoteApis(this BffFrontend frontend, params RemoteApi[] remoteApis) =>
        // todo: EV: check for duplicate routes

        frontend with
        {
            Proxy = frontend.Proxy with
            {
                RemoteApis = frontend.Proxy
                    .RemoteApis
                    .Union(remoteApis)
                    .ToArray()
            }
        };
}
