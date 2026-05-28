// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml.Configuration;

/// <summary>
/// Configuration options for a standalone SAML 2.0 Service Provider authentication
/// scheme, registered via the AddSamlServiceProvider extension methods on
/// AuthenticationBuilder.
/// </summary>
public sealed class SamlServiceProviderOptions
{
    /// <summary>
    /// The entity ID of this Service Provider. Required.
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    /// The module path that the Saml2 handler intercepts for ACS and metadata
    /// callbacks. Defaults to <c>/Saml2</c>.
    /// </summary>
    public string ModulePath { get; set; } = SamlServiceProviderDefaults.ModulePath;

    /// <summary>
    /// Authentication scheme to sign in with to establish a session after SAML
    /// authentication completes. When <c>null</c>, the default sign-in scheme
    /// is used.
    /// </summary>
    public string? SignInScheme { get; set; }

    /// <summary>
    /// Authentication scheme to sign out with when a logout request is received
    /// from the identity provider. When <c>null</c>, the default sign-out scheme
    /// is used.
    /// </summary>
    public string? SignOutScheme { get; set; }

    /// <summary>
    /// The signing algorithm to use for outbound SAML requests. Defaults to
    /// <c>http://www.w3.org/2001/04/xmldsig-more#rsa-sha256</c>.
    /// </summary>
    public string OutboundSigningAlgorithm { get; set; } =
        "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

    /// <summary>
    /// Whether assertions from the identity provider must be signed. Defaults to
    /// <c>true</c>.
    /// </summary>
    public bool WantAssertionsSigned { get; set; } = true;

    /// <summary>
    /// The entity ID of the remote SAML identity provider. Required.
    /// </summary>
    public string? IdpEntityId { get; set; }

    /// <summary>
    /// The URL of the Single Sign-On service on the remote identity provider.
    /// </summary>
    public string? SingleSignOnServiceUrl { get; set; }

    /// <summary>
    /// The URL of the Single Logout service on the remote identity provider.
    /// When not set, outbound logout requests are disabled.
    /// </summary>
    public string? SingleLogoutServiceUrl { get; set; }

    /// <summary>
    /// Base64-encoded X.509 certificates used to validate signatures from the
    /// remote identity provider. Multiple certificates can be provided to
    /// support key rotation.
    /// </summary>
    public IList<string> SigningCertificatesBase64 { get; set; } = new List<string>();

    /// <summary>
    /// The SAML binding type to use when sending authentication requests.
    /// Defaults to <see cref="SamlBindingType.HttpRedirect"/>.
    /// </summary>
    public SamlBindingType BindingType { get; set; } = SamlBindingType.HttpRedirect;

    /// <summary>
    /// Whether to allow unsolicited (IdP-initiated) authentication responses.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AllowUnsolicitedAuthnResponse { get; set; }

    /// <summary>
    /// Base64-encoded X.509 certificate (with private key, PKCS#12) used by the SP to sign
    /// outbound SAML messages (AuthnRequests, LogoutResponses). Required when the remote
    /// IdP expects signed requests or when single logout is used.
    /// </summary>
    public string? SpSigningCertificateBase64 { get; set; }

    /// <summary>
    /// Optional password for the PKCS#12 SP signing certificate.
    /// </summary>
    public string? SpSigningCertificatePassword { get; set; }
}
