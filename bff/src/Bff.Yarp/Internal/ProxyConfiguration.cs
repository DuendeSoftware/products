// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Yarp.Internal;

internal sealed record ProxyConfiguration
{
    public Dictionary<string, FrontendProxyConfiguration> Frontends { get; internal init; } = new();
}
