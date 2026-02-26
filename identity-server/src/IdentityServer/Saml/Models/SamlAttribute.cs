// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents a SAML 2.0 Attribute element
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - SamlAttribute is the standard SAML term
public record SamlAttribute
#pragma warning restore CA1711
{
    /// <summary>
    /// Attribute name (e.g., "urn:oid:0.9.2342.19200300.100.1.1" or "email")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Attribute name format URI (e.g., "urn:oasis:names:tc:SAML:2.0:attrname-format:uri")
    /// </summary>
    public string? NameFormat { get; set; }

    /// <summary>
    /// Human-readable friendly name for the attribute (e.g., "uid", "email").
    /// Optional but recommended for debugging and some SP compatibility.
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Attribute values (can be multi-valued)
    /// </summary>
#pragma warning disable CA2227, CA1002 // Collection properties should be read only and use Collection<T> - List<T> is by design for mutability and performance
    public List<string> Values { get; set; } = [];
#pragma warning restore CA2227, CA1002
}
