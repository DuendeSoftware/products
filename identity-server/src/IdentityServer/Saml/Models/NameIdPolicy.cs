// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents the NameIDPolicy element from a SAML AuthnRequest.
/// Specifies constraints on the name identifier to be returned.
/// SAML 2.0 Core Section 3.4.1.1
/// </summary>
public record NameIdPolicy
{
    /// <summary>
    /// Gets the requested name identifier format.
    /// Example: "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent"
    /// If null, no specific format is requested.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Gets the SPNameQualifier to use in the returned NameID.
    /// Typically the SP's entity ID, but SP can request a different value.
    /// If null, IdP should use SP's entity ID.
    /// </summary>
    public string? SPNameQualifier { get; init; }

    internal static class AttributeNames
    {
        public const string Format = "Format";
        public const string SPNameQualifier = "SPNameQualifier";
    }
}
