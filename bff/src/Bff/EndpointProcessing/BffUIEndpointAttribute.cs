// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Marks an endpoint as BFF UI endpoint.
/// This implies that it is not intended for Ajax requests.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal class BffUiEndpointAttribute : Attribute, IBffUIApiEndpoint
{
}
