// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Models;

public record SamlError
{
    public required string StatusCode { get; init; }
    public string? SubStatusCode { get; init; }
    public required string Message { get; init; }
}
