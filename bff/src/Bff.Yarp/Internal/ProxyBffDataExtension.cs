// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;

namespace Duende.Bff.Yarp.Internal;

internal record ProxyBffDataExtension : IBffDataExtension
{
    internal RemoteApi[] RemoteApis { get; init; } = [];
}
