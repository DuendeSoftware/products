// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable
namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents a SAML 2.0 NameID element
/// </summary>
internal record NameIdentifier
{
    /// <summary>
    /// The name identifier value
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// The format of the name identifier (URI)
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// The NameQualifier attribute
    /// </summary>
    public string? NameQualifier { get; set; }

    /// <summary>
    /// The SPNameQualifier attribute
    /// </summary>
    public string? SPNameQualifier { get; set; }
}
