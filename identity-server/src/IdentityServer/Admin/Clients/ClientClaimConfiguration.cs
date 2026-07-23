// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// A claim included in tokens for this client.
/// </summary>
public sealed class ClientClaimConfiguration
{
    /// <summary>Claim type.</summary>
    public required string Type { get; init; }

    /// <summary>Claim value.</summary>
    public required string Value { get; init; }

    /// <summary>Claim value type. Defaults to string.</summary>
    public string ValueType { get; init; } = ClaimValueTypes.String;
}
