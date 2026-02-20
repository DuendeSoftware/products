// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Well-known SAML 2.0 status code URNs as defined in the SAML 2.0 Core specification.
/// </summary>
public static class SamlStatusCodes
{
    /// <summary>The request succeeded.</summary>
    public const string Success = "urn:oasis:names:tc:SAML:2.0:status:Success";

    /// <summary>The request could not be performed due to an error on the part of the requester.</summary>
    public const string Requester = "urn:oasis:names:tc:SAML:2.0:status:Requester";

    /// <summary>The request could not be performed due to an error on the part of the SAML responder or SAML authority.</summary>
    public const string Responder = "urn:oasis:names:tc:SAML:2.0:status:Responder";

    /// <summary>The SAML responder could not process the request because the version of the request message was incorrect.</summary>
    public const string VersionMismatch = "urn:oasis:names:tc:SAML:2.0:status:VersionMismatch";

    /// <summary>The responding provider cannot authenticate the principal by means of the currently deployed authentication authority.</summary>
    public const string NoAuthnContext = "urn:oasis:names:tc:SAML:2.0:status:NoAuthnContext";

    /// <summary>The authentication attempt failed.</summary>
    public const string AuthnFailed = "urn:oasis:names:tc:SAML:2.0:status:AuthnFailed";

    /// <summary>The responding provider cannot permit a subject confirmation based on the requirements of the requester.</summary>
    public const string InvalidNameIdPolicy = "urn:oasis:names:tc:SAML:2.0:status:InvalidNameIDPolicy";

    /// <summary>The SAML responder or SAML authority is able to process the request but has chosen not to respond.</summary>
    public const string RequestDenied = "urn:oasis:names:tc:SAML:2.0:status:RequestDenied";

    /// <summary>The responding provider does not recognize the principal specified or implied by the request.</summary>
    public const string UnknownPrincipal = "urn:oasis:names:tc:SAML:2.0:status:UnknownPrincipal";

    /// <summary>The SAML responder cannot properly fulfill the request using the protocol binding specified in the request.</summary>
    public const string UnsupportedBinding = "urn:oasis:names:tc:SAML:2.0:status:UnsupportedBinding";

    /// <summary>The identity provider cannot authenticate the presenter in a manner that satisfies the IsPassive constraint of the request.</summary>
    public const string NoPassive = "urn:oasis:names:tc:SAML:2.0:status:NoPassive";
}
