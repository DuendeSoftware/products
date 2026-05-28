// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using System.Security.Cryptography.Xml;

namespace Duende.IdentityServer.Saml;

public static class SamlConstants
{
    /// <summary>
    /// Profile service caller identifier.
    /// </summary>
    internal const string SsoResponseProfileCaller = "Saml2SsoResponseGenerator";

    /// <summary>
    /// Default values
    /// </summary>
    public static class Defaults
    {
        /// <summary>
        /// Default path for Saml2
        /// </summary>
        public const string Saml2Path = "/Saml2";

        /// <summary>
        /// Default path for SSO endpoint
        /// </summary>
        public const string SingleSignOnServicePath = "/Saml2/SSO";

        /// <summary>
        /// Default path for SSO callback endpoint (post-login return)
        /// </summary>
        public const string SingleSignOnCallbackPath = "/Saml2/SSO/Callback";

        /// <summary>
        /// Default path for SLO endpoint
        /// </summary>
        public const string SingleLogoutServicePath = "/Saml2/SLO";

        /// <summary>
        /// Default path for SLO callback endpoint (post-logout return)
        /// </summary>
        public const string SingleLogoutCallbackPath = "/Saml2/SLO/Callback";
    }

    public static class RequestProperties
    {
        public const string SAMLRequest = "SAMLRequest";
        public const string SAMLResponse = "SAMLResponse";
        public const string RelayState = "RelayState";
        public const string Signature = "Signature";
        public const string SigAlg = "SigAlg";
    }

    public static class ContentTypes
    {
        /// <summary>
        /// https://www.iana.org/assignments/media-types/application/samlmetadata+xml
        /// </summary>
        public const string Metadata = "application/samlmetadata+xml";
    }

    public static class Namespaces
    {
        public const string SamlPrefix = "saml";
        public const string Assertion = "urn:oasis:names:tc:SAML:2.0:assertion";
        public const string SamlpPrefix = "samlp";
        public const string Protocol = "urn:oasis:names:tc:SAML:2.0:protocol";
        public const string MetadataPrefix = "md";
        public const string Metadata = "urn:oasis:names:tc:SAML:2.0:metadata";
        public const string XmlSignaturePrefix = "ds";
        public const string XmlSignature = "http://www.w3.org/2000/09/xmldsig#";
        public const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    }

    public static class NameIdentifierFormats
    {
        public const string EmailAddress = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";
        public const string Persistent = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent";
        public const string Transient = "urn:oasis:names:tc:SAML:2.0:nameid-format:transient";
        public const string Unspecified = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";
    }

    public static class Bindings
    {
        public const string HttpRedirect = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";
        public const string HttpPost = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";
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

    public static class LogoutReasons
    {
        public const string User = "urn:oasis:names:tc:SAML:2.0:logout:user";
        public const string Admin = "urn:oasis:names:tc:SAML:2.0:logout:admin";
        public const string GlobalTimeout = "urn:oasis:names:tc:SAML:2.0:logout:global-timeout";
    }

    /// <summary>
    /// Well-known X.509 public key algorithm OID values.
    /// </summary>
    internal static class KeyAlgorithmOids
    {
        /// <summary>OID 1.2.840.113549.1.1.1 — RSA (PKCS#1).</summary>
        internal const string Rsa = "1.2.840.113549.1.1.1";

        /// <summary>OID 1.2.840.10045.2.1 — id-ecPublicKey (used by both ECDSA and ECDH).</summary>
        internal const string EcPublicKey = "1.2.840.10045.2.1";
    }

    /// <summary>
    /// ECDSA signature algorithm URIs (not provided as constants by <see cref="SignedXml"/>).
    /// </summary>
    public static class EcdsaAlgorithms
    {
        public const string EcdsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
        public const string EcdsaSha384 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha384";
        public const string EcdsaSha512 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha512";
    }

    /// <summary>
    /// Default allowed algorithms.
    /// </summary>
    public static readonly IEnumerable<string> DefaultAllowedAlgorithms =
        new ReadOnlyCollection<string>(
        [
            SignedXml.XmlDsigSHA256Url,
            SignedXml.XmlDsigSHA384Url,
            SignedXml.XmlDsigSHA512Url,
            SignedXml.XmlDsigRSASHA256Url,
            SignedXml.XmlDsigRSASHA384Url,
            SignedXml.XmlDsigRSASHA512Url,
            EcdsaAlgorithms.EcdsaSha256,
            EcdsaAlgorithms.EcdsaSha384,
            EcdsaAlgorithms.EcdsaSha512,
        ]);

    public static class SubjectConfirmationMethods
    {
        public const string Bearer = "urn:oasis:names:tc:SAML:2.0:cm:bearer";
    }

    public static class AuthnContextClasses
    {
        public const string PasswordProtectedTransport = "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport";
        public const string Unspecified = "urn:oasis:names:tc:SAML:2.0:ac:classes:unspecified";
    }

    /// <summary>
    /// Names of elements
    /// </summary>
    /// <remarks>The naming of the constants are deriberately not following
    /// casing convention in order to be exactly the same as the contents.
    /// </remarks>
    public static class Elements
    {
        public const string Advice = nameof(Advice);
        public const string AttributeAuthorityDescriptor = nameof(AttributeAuthorityDescriptor);
        public const string ArtifactResolutionService = nameof(ArtifactResolutionService);
        public const string Assertion = nameof(Assertion);
        public const string Attribute = nameof(Attribute);
        public const string AttributeStatement = nameof(AttributeStatement);
        public const string AttributeValue = nameof(AttributeValue);
        public const string Audience = nameof(Audience);
        public const string AudienceRestriction = nameof(AudienceRestriction);
        public const string AuthnAuthorityDescriptor = nameof(AuthnAuthorityDescriptor);
        public const string AuthnContext = nameof(AuthnContext);
        public const string AuthnContextClassRef = nameof(AuthnContextClassRef);
        public const string AuthnContextDeclRef = nameof(AuthnContextDeclRef);
        public const string AuthnRequest = nameof(AuthnRequest);
        public const string LogoutRequest = nameof(LogoutRequest);
        public const string LogoutResponse = nameof(LogoutResponse);
        public const string AuthnStatement = nameof(AuthnStatement);
        public const string AuthzDecisionStatement = nameof(AuthzDecisionStatement);
        public const string Conditions = nameof(Conditions);
        public const string ContactPerson = nameof(ContactPerson);
        public const string EntityDescriptor = nameof(EntityDescriptor);
        public const string Extensions = nameof(Extensions);
        public const string GetComplete = nameof(GetComplete);
        public const string IDPEntry = nameof(IDPEntry);
        public const string IDPList = nameof(IDPList);
        public const string IDPSSODescriptor = nameof(IDPSSODescriptor);
        public const string Issuer = nameof(Issuer);
        public const string KeyDescriptor = nameof(KeyDescriptor);
        public const string KeyInfo = nameof(KeyInfo);
        public const string ManageNameIDService = nameof(ManageNameIDService);
        public const string NameID = nameof(NameID);
        public const string NameIDFormat = nameof(NameIDFormat);
        public const string NameIDPolicy = nameof(NameIDPolicy);
        public const string OneTimeUse = nameof(OneTimeUse);
        public const string Organization = nameof(Organization);
        public const string PDPDescriptor = nameof(PDPDescriptor);
        public const string ProxyRestriction = nameof(ProxyRestriction);
        public const string Response = nameof(Response);
        public const string RequestedAuthnContext = nameof(RequestedAuthnContext);
        public const string RequesterID = nameof(RequesterID);
        public const string RoleDescriptor = nameof(RoleDescriptor);
        public const string Scoping = nameof(Scoping);
        public const string Signature = nameof(Signature);
        public const string SingleLogoutService = nameof(SingleLogoutService);
        public const string SessionIndex = nameof(SessionIndex);
        public const string SingleSignOnService = nameof(SingleSignOnService);
        public const string SPSSODescriptor = nameof(SPSSODescriptor);
        public const string Status = nameof(Status);
        public const string StatusCode = nameof(StatusCode);
        public const string StatusMessage = nameof(StatusMessage);
        public const string Subject = nameof(Subject);
        public const string SubjectConfirmation = nameof(SubjectConfirmation);
        public const string SubjectConfirmationData = nameof(SubjectConfirmationData);
        public const string SubjectLocality = nameof(SubjectLocality);
    }

    /// <summary>
    /// Names of attributes.
    /// </summary>
    /// <remarks>The naming of the constants are deriberately not following
    /// casing convention in order to be exactly the same as the contents.
    /// </remarks>
    public static class Attributes
    {
        public const string Address = nameof(Address);
        public const string AllowCreate = nameof(AllowCreate);
        public const string AssertionConsumerServiceIndex = nameof(AssertionConsumerServiceIndex);
        public const string AssertionConsumerServiceURL = nameof(AssertionConsumerServiceURL);
        public const string AttributeConsumingServiceIndex = nameof(AttributeConsumingServiceIndex);
        public const string AuthnInstant = nameof(AuthnInstant);
        public const string Binding = nameof(Binding);
        public const string cacheDuration = nameof(cacheDuration);
        public const string Comparison = nameof(Comparison);
        public const string Consent = nameof(Consent);
        public const string Destination = nameof(Destination);
        public const string entityID = nameof(entityID);
        public const string ForceAuthn = nameof(ForceAuthn);
        public const string Format = nameof(Format);
        public const string FriendlyName = nameof(FriendlyName);
        public const string ID = nameof(ID);
        public const string index = nameof(index);
        public const string isDefault = nameof(isDefault);
        public const string IsPassive = nameof(IsPassive);
        public const string InResponseTo = nameof(InResponseTo);
        public const string IssueInstant = nameof(IssueInstant);
        public const string Loc = nameof(Loc);
        public const string Location = nameof(Location);
        public const string Method = nameof(Method);
        public const string Name = nameof(Name);
        public const string NameFormat = nameof(NameFormat);
        public const string NameQualifier = nameof(NameQualifier);
        public const string nil = nameof(nil);
        public const string NotBefore = nameof(NotBefore);
        public const string NotOnOrAfter = nameof(NotOnOrAfter);
        public const string ProtocolBinding = nameof(ProtocolBinding);
        public const string protocolSupportEnumeration = nameof(protocolSupportEnumeration);
        public const string ProviderID = nameof(ProviderID);
        public const string ProviderName = nameof(ProviderName);
        public const string ProxyCount = nameof(ProxyCount);
        public const string Recipient = nameof(Recipient);
        public const string Reason = nameof(Reason);
        public const string SessionIndex = nameof(SessionIndex);
        public const string SessionNotOnOrAfter = nameof(SessionNotOnOrAfter);
        public const string SPNameQualifier = nameof(SPNameQualifier);
        public const string use = nameof(use);
        public const string validUntil = nameof(validUntil);
        public const string Value = nameof(Value);
        public const string Version = nameof(Version);
        public const string WantAuthnRequestsSigned = nameof(WantAuthnRequestsSigned);
    }
}
