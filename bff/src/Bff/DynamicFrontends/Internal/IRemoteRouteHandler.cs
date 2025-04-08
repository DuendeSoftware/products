// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends.Internal;

/// <summary>
/// Handles remote routes, such as those defined in the BFF configuration or YARP routes.
/// </summary>
internal interface IRemoteRouteHandler
{
    Task<bool> HandleAsync(HttpContext context, CT ct = default);
}
