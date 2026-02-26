// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml;

internal static class SamlConstants
{
    internal class Urls
    {
        public const string SamlRoute = "/saml";
        public const string Metadata = "/metadata";
        public const string SignIn = "/signin";
        public const string SigninCallback = "/signin_callback";
        public const string IdpInitiated = "/idp-initiated";
        public const string Signout = "/signout";
        public const string SingleLogout = "/logout";
        public const string SingleLogoutCallback = "/logout_callback";
    }

    internal class RequestProperties
    {
        public const string SAMLRequest = "SAMLRequest";
        public const string SAMLResponse = "SAMLResponse";
        public const string RelayState = "RelayState";
        public const string Signature = "Signature";
        public const string SigAlg = "SigAlg";
    }

    internal class ContentTypes
    {
        /// <summary>
        /// https://www.iana.org/assignments/media-types/application/samlmetadata+xml
        /// </summary>
        public const string Metadata = "application/samlmetadata+xml";
    }
    internal static class Namespaces
    {
        public const string Assertion = "urn:oasis:names:tc:SAML:2.0:assertion";
        public const string Protocol = "urn:oasis:names:tc:SAML:2.0:protocol";
        public const string Metadata = "urn:oasis:names:tc:SAML:2.0:metadata";
        public const string XmlSignature = "http://www.w3.org/2000/09/xmldsig#";
        public const string XmlEncryption = "http://www.w3.org/2001/04/xmlenc#";
    }

    internal static class NameIdentifierFormats
    {
        public const string EmailAddress = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";
        public const string Persistent = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent";
        public const string Transient = "urn:oasis:names:tc:SAML:2.0:nameid-format:transient";
        public const string Unspecified = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";
    }

    internal static class Bindings
    {
        public const string HttpRedirect = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";
        public const string HttpPost = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";
    }

    internal static class MetadataElements
    {
        public const string EntityDescriptor = "EntityDescriptor";
        public const string IdpSsoDescriptor = "IDPSSODescriptor";
        public const string KeyDescriptor = "KeyDescriptor";
        public const string KeyInfo = "KeyInfo";
        public const string X509Data = "X509Data";
        public const string X509Certificate = "X509Certificate";
        public const string NameIdFormat = "NameIDFormat";
        public const string SingleSignOnService = "SingleSignOnService";
        public const string SingleLogoutService = "SingleLogoutService";
    }

    internal static class AuthenticationRequestAttributes
    {
        public const string RootElementName = "AuthnRequest";
    }

    internal static class MetadataAttributes
    {
        public const string EntityId = "entityID";
        public const string ValidUntil = "validUntil";
        public const string ProtocolSupportEnumeration = "protocolSupportEnumeration";
        public const string WantAuthnRequestsSigned = "WantAuthnRequestsSigned";
        public const string Use = "use";
        public const string Binding = "Binding";
        public const string Location = "Location";

        /// <summary>
        /// Converts a KeyUse enum value to its string representation.
        /// </summary>
        internal static string ToString(KeyUse keyUse) => keyUse switch
        {
            KeyUse.Signing => "signing",
            KeyUse.Encryption => "encryption",
            _ => throw new ArgumentOutOfRangeException(nameof(keyUse), keyUse, "Unknown key use")
        };
    }

    public static class AttributeNameFormats
    {
        /// <summary>
        /// Attribute name is interpreted as a URI reference (most common for OID format)
        /// </summary>
        public const string Uri = "urn:oasis:names:tc:SAML:2.0:attrname-format:uri";
    }

    public static class ClaimTypes
    {
        public const string AuthnContextClassRef = "saml:acr";
    }

    internal static class LogoutReasons
    {
        public const string User = "urn:oasis:names:tc:SAML:2.0:logout:user";
        public const string Admin = "urn:oasis:names:tc:SAML:2.0:logout:admin";
        public const string GlobalTimeout = "urn:oasis:names:tc:SAML:2.0:logout:global-timeout";
    }
}
