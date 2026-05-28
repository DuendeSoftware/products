// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Saml Attribute, Core 2.7.3.1
/// </summary>
[DebuggerDisplay("{Name,nq}:{AllValues}")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix — 'Attribute' is the correct SAML domain term
public class SamlAttribute
#pragma warning restore CA1711
{
    /// <summary>
    /// Name of attribute
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Attribute name format URI (e.g., "urn:oasis:names:tc:SAML:2.0:attrname-format:uri").
    /// </summary>
    public string? NameFormat { get; set; }

    /// <summary>
    /// Human-readable friendly name for the attribute (e.g., "uid", "email").
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Attribute values.
    /// </summary>
    public List<string?> Values { get; init; } = [];

    private string AllValues => string.Join(", ", Values);
}
