// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff.Yarp;

internal static class EventIds
{
    public static readonly EventId ProxyError = new(5, "ProxyError");
}
