// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents a SAML 2.0 status code as defined in the SAML 2.0 Core specification.
/// </summary>
public readonly record struct SamlStatusCode(string Value)
{
    public static readonly SamlStatusCode Success = new("urn:oasis:names:tc:SAML:2.0:status:Success");
    public static readonly SamlStatusCode Requester = new("urn:oasis:names:tc:SAML:2.0:status:Requester");
    public static readonly SamlStatusCode Responder = new("urn:oasis:names:tc:SAML:2.0:status:Responder");
    public static readonly SamlStatusCode VersionMismatch = new("urn:oasis:names:tc:SAML:2.0:status:VersionMismatch");
    public static readonly SamlStatusCode NoAuthnContext = new("urn:oasis:names:tc:SAML:2.0:status:NoAuthnContext");
    public static readonly SamlStatusCode AuthnFailed = new("urn:oasis:names:tc:SAML:2.0:status:AuthnFailed");
    public static readonly SamlStatusCode InvalidNameIdPolicy = new("urn:oasis:names:tc:SAML:2.0:status:InvalidNameIDPolicy");
    public static readonly SamlStatusCode RequestDenied = new("urn:oasis:names:tc:SAML:2.0:status:RequestDenied");
    public static readonly SamlStatusCode UnknownPrincipal = new("urn:oasis:names:tc:SAML:2.0:status:UnknownPrincipal");
    public static readonly SamlStatusCode UnsupportedBinding = new("urn:oasis:names:tc:SAML:2.0:status:UnsupportedBinding");
    public static readonly SamlStatusCode NoPassive = new("urn:oasis:names:tc:SAML:2.0:status:NoPassive");

    /// <inheritdoc />
    public override string ToString() => Value;

    public static implicit operator string(SamlStatusCode statusCode) => statusCode.Value;

    public static implicit operator SamlStatusCode(string value) => new(value);

    public SamlStatusCode ToSamlStatusCode() => Value;
}
