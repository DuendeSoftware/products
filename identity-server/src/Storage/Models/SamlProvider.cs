// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a SAML 2.0 identity provider for use with dynamic provider infrastructure.
/// </summary>
public record SamlProvider : IdentityProvider
{
    /// <summary>
    /// Ctor
    /// </summary>
    public SamlProvider() : base("saml")
    {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    public SamlProvider(IdentityProvider other) : base("saml", other)
    {
    }

    /// <summary>
    /// The entity ID of the remote SAML identity provider.
    /// </summary>
    public string? IdpEntityId
    {
        get => this["IdpEntityId"];
        set => this["IdpEntityId"] = value;
    }

    /// <summary>
    /// The URL of the Single Sign-On service on the remote identity provider.
    /// </summary>
    public string? SingleSignOnServiceUrl
    {
        get => this["SingleSignOnServiceUrl"];
        set => this["SingleSignOnServiceUrl"] = value;
    }

    /// <summary>
    /// The URL of the Single Logout service on the remote identity provider.
    /// </summary>
    public string? SingleLogoutServiceUrl
    {
        get => this["SingleLogoutServiceUrl"];
        set => this["SingleLogoutServiceUrl"] = value;
    }

    /// <summary>
    /// Base64-encoded X.509 certificate used to validate signatures from the
    /// remote identity provider.
    /// </summary>
    public string? SigningCertificateBase64
    {
        get => this["SigningCertificateBase64"];
        set => this["SigningCertificateBase64"] = value;
    }

    /// <summary>
    /// Base64-encoded X.509 certificate (with private key, PKCS#12) used by the SP to sign
    /// outbound SAML messages (AuthnRequests, LogoutResponses). Required when the remote
    /// IdP expects signed requests or when single logout is used.
    /// </summary>
    public string? SpSigningCertificateBase64
    {
        get => this["SpSigningCertificateBase64"];
        set => this["SpSigningCertificateBase64"] = value;
    }

    /// <summary>
    /// Optional password for the PKCS#12 SP signing certificate.
    /// </summary>
    public string? SpSigningCertificatePassword
    {
        get => this["SpSigningCertificatePassword"];
        set => this["SpSigningCertificatePassword"] = value;
    }

    /// <summary>
    /// The SAML binding type to use when sending authentication requests.
    /// Accepted values: "redirect" (default) or "post".
    /// </summary>
    public string BindingType
    {
        get => this["BindingType"] ?? "redirect";
        set => this["BindingType"] = value;
    }

    /// <summary>
    /// Optional override for the SP entity ID. When not set, IdentityServer's
    /// issuer URI is used.
    /// </summary>
    public string? SpEntityId
    {
        get => this["SpEntityId"];
        set => this["SpEntityId"] = value;
    }

    /// <summary>
    /// Whether to allow unsolicited (IdP-initiated) authentication responses.
    /// Defaults to false.
    /// </summary>
    public bool AllowUnsolicitedAuthnResponse
    {
        get => "true".Equals(this["AllowUnsolicitedAuthnResponse"], StringComparison.Ordinal);
        set => this["AllowUnsolicitedAuthnResponse"] = value ? "true" : "false";
    }

    /// <summary>
    /// Whether assertions from the identity provider must be signed.
    /// Defaults to true.
    /// </summary>
    public bool WantAssertionsSigned
    {
        get => this["WantAssertionsSigned"] == null || "true".Equals(this["WantAssertionsSigned"], StringComparison.Ordinal);
        set => this["WantAssertionsSigned"] = value ? "true" : "false";
    }

    /// <summary>
    /// The signing algorithm to use for outbound SAML requests. Defaults to
    /// "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256".
    /// </summary>
    public string OutboundSigningAlgorithm
    {
        get => this["OutboundSigningAlgorithm"] ?? "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        set => this["OutboundSigningAlgorithm"] = value;
    }
}
