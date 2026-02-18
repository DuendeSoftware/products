// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml.Models;

public record EndpointType
{
    public required Uri Location { get; init; }

    public required SamlBinding Binding { get; init; }
}
