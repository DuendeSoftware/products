// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Status codes, mapped against states in section 3.2.2.2 in the SAML2 spec.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    internal enum Saml2StatusCode
    {
        /// <summary>
        /// Success.
        /// </summary>
        Success = 1,

        /// <summary>
        /// Error because of the requester.
        /// </summary>
        Requester = 2,

        /// <summary>
        /// Error because of the responder.
        /// </summary>
        Responder = 3,

        /// <summary>
        /// Versions doesn't match.
        /// </summary>
        VersionMismatch = 4,

        /// <summary>
        /// The responding provider was unable to successfully authenticate the principal
        /// </summary>
        AuthnFailed = 5,

        /// <summary>
        /// Unexpected or invalid content was encountered within a saml:Attribute or saml:AttributeValue element.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Attr")]
        InvalidAttrNameOrValue = 6,

        /// <summary>
        /// The responding provider cannot or will not support the requested name identifier policy.
        /// </summary>
        InvalidNameIdPolicy = 7,

        /// <summary>
        /// The specified authentication context requirements cannot be met by the responder.
        /// </summary>
        NoAuthnContext = 8,

        /// <summary>
        /// Used by an intermediary to indicate that none of the supported identity provider Loc elements in
        /// an IDPList can be resolved or that none of the supported identity providers are available.
        /// </summary>
        NoAvailableIdp = 9,

        /// <summary>
        /// Indicates the responding provider cannot authenticate the principal passively, as has been requested.
        /// </summary>
        NoPassive = 10,

        /// <summary>
        /// Used by an intermediary to indicate that none of the identity providers in an IDPList are supported by the intermediary.
        /// </summary>
        NoSupportedIdp = 11,

        /// <summary>
        /// Used by a session authority to indicate to a session participant that it was not able to propagate logout to all other session participants.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        PartialLogout = 12,

        /// <summary>
        /// Indicates that a responding provider cannot authenticate the principal directly and is not permitted to proxy the request further.
        /// </summary>
        ProxyCountExceeded = 13,

        /// <summary>
        /// The SAML responder or SAML authority is able to process the request but has chosen not to
        /// respond. This status code MAY be used when there is concern about the security context of the
        /// request message or the sequence of request messages received from a particular requester.
        /// </summary>
        RequestDenied = 14,

        /// <summary>
        /// The SAML responder or SAML authority does not support the request.
        /// </summary>
        RequestUnsupported = 15,

        /// <summary>
        /// The SAML responder cannot process any requests with the protocol version specified in the request.
        /// </summary>
        RequestVersionDeprecated = 16,

        /// <summary>
        /// The SAML responder cannot process the request because the protocol version specified in the
        /// request message is a major upgrade from the highest protocol version supported by the responder.
        /// </summary>
        RequestVersionTooHigh = 17,

        /// <summary>
        /// The SAML responder cannot process the request because the protocol version specified in the
        /// request message is too low.
        /// </summary>
        RequestVersionTooLow = 18,

        /// <summary>
        /// The resource value provided in the request message is invalid or unrecognized.
        /// </summary>
        ResourceNotRecognized = 19,

        /// <summary>
        /// The response message would contain more elements than the SAML responder is able to return.
        /// </summary>
        TooManyResponses = 20,

        /// <summary>
        /// An entity that has no knowledge of a particular attribute profile has been presented with an attribute
        /// drawn from that profile.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Attr")]
        UnknownAttrProfile = 21,

        /// <summary>
        /// The responding provider does not recognize the principal specified or implied by the request.
        /// </summary>
        UnknownPrincipal = 22,

        /// <summary>
        /// The SAML responder cannot properly fulfill the request using the protocol binding specified in the
        /// request.
        /// </summary>
        UnsupportedBinding = 23
    }
}
