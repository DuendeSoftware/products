// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents a SAML version string.
/// </summary>
public readonly record struct SamlVersion(string Value)
{
    public static readonly SamlVersion V2 = new("2.0");

    /// <inheritdoc />
    public override string ToString() => Value;

    public static implicit operator SamlVersion(string value) => new(value);

    public SamlVersion ToSamlVersion() => Value;
}
