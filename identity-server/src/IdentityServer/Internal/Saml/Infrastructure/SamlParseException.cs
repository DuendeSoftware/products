// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA1064 // Exceptions should be public
internal class SamlParseException : Exception
#pragma warning restore CA1064
#pragma warning restore CA1032
{
    /// <summary>
    /// The Issuer value extracted from the XML, if available.
    /// FOR SERVER-SIDE LOGGING ONLY. Do not use for SP lookup or SAML response generation.
    /// </summary>
    internal string? Issuer { get; }

    internal SamlParseException(string message, Exception innerException, string? issuer = null)
        : base(message, innerException) => Issuer = issuer;

    internal SamlParseException(string message, string? issuer = null)
        : base(message) => Issuer = issuer;
}
