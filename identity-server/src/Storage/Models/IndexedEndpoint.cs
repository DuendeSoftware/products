// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Models;

public record IndexedEndpoint : SamlEndpointType
{
    /// <summary>
    /// Index of the endpoint.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Indicates this is the default endpoint.
    /// </summary>
    public bool IsDefault { get; init; }
}
