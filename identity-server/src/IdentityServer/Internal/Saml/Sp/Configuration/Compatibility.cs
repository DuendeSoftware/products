// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// Compatibility settings. Can be used to make Saml2 accept
    /// certain non-standard behaviour.
    /// </summary>
    internal class Compatibility
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public Compatibility()
        { }

        /// <summary>
        /// If an EntitiesDescriptor element is found when loading metadata
        /// for an IdentityProvider, automatically check inside it if there
        /// is a single EntityDescriptor and in that case use it.
        /// </summary>
        public bool UnpackEntitiesDescriptorInIdentityProviderMetadata { get; set; }

        /// <summary>
        /// Do not send logout state cookie, e.g. if you are not using ReturnUrl
        /// or if you know the cookie will be lost due to cross-domain redirects
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public bool DisableLogoutStateCookie { get; set; }

        /// <summary>
        /// Honor the owin authentication mode even on logout. Normally the logout
        /// handling is always done as if the middleware was active, to allow for
        /// simple sign out without specifying an auth type.
        /// </summary>
        public bool StrictOwinAuthenticationMode { get; set; }

        /// <summary>
        /// Do not read the AuthnContext element in Saml2Response.
        /// If you do not need these values to be present as claims in the generated
        /// identity, using this option can prevent XML format errors (IDX13102)
        /// e.g. when value cannot parse as absolute URI
        /// </summary>
        public bool IgnoreAuthenticationContextInResponse { get; set; }

        /// <summary>
        /// Ignore the check for the missing InResponseTo attribute in the Saml response.
        /// This is different to setting the allowUnsolicitedAuthnResponse as it will only
        /// ignore the InResponseTo attribute if there is no relayState. Setting
        /// IgnoreMissingInResponseTo to true will always skip the check.
        /// </summary>
        public bool IgnoreMissingInResponseTo { get; set; }

        /// <summary>
        /// Handling logout requires access to the authenticated user session. If logout
        /// is done over the POST binding, the session cookie must have SameSite=None
        /// set (which is probably a bad idea). To avoid problems, disable logout
        /// over POST in metadata by default.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public bool EnableLogoutOverPost { get; set; }

        /// <summary>
        /// SAML2 Specs says in section 4.4.4.2:
        /// "... The responder MUST authenticate itself to the requester and ensure message integrity, either by signing the message or using a binding-specific mechanism."
        /// 
        /// Unfortunately not all IDP seem to follow the specification. Disables requirement for a signed LogoutResponse.
        /// </summary>
        public bool AcceptUnsignedLogoutResponses { get; set; }
    }
}
