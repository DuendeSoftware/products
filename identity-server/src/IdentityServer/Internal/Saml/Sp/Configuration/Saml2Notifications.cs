// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Claims;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// Set of callbacks that can be used as extension points for various
    /// events.
    /// </summary>
    internal class Saml2Notifications
    {
        /// <summary>
        /// Notification called when a <see cref="Saml2AuthenticationRequest"/>
        /// has been created. The authenticationrequest can be amended and
        /// modified.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public Action<Saml2AuthenticationRequest, IdentityProvider, IDictionary<string, string>>
            AuthenticationRequestCreated
        { get; set; } = (request, provider, dictionary) => { };

        public Action<Saml2AuthenticationRequest, XDocument, Saml2BindingType>
            AuthenticationRequestXmlCreated
        { get; set; } = (request, xDocument, Saml2BindingType) => { };

        /// <summary>
        /// Notification called when the SignIn command has produced a
        /// <see cref="CommandResult"/>, but before anything has been applied
        /// to the outgoing response. Set the <see cref="CommandResult.HandledResult"/>
        /// flag to suppress the library's built in apply functionality to the
        /// outgoing response.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public Action<CommandResult, IDictionary<string, string>>
            SignInCommandResultCreated
        { get; set; } = (cr, r) => { };

        /// <summary>
        /// Notification called when the SignIn command is about to select
        /// what Idp to use for the request. The EntityId is the one supplied
        /// (e.g. through query string). To select a specicic IdentityProvider
        /// simply return it. Return <code>null</code> to fall back to built
        /// in selection.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public Func<EntityId, IDictionary<string, string>, IdentityProvider>
            SelectIdentityProvider
        { get; set; } = (ei, r) => null;

        /// <summary>
        /// Notification called to decide if a SameSite=None attribute should
        /// be set for a cookie. The default implementation is based on the pseudo
        /// code in https://devblogs.microsoft.com/aspnet/upcoming-samesite-cookie-changes-in-asp-net-and-asp-net-core/
        /// More covering code can be found at 
        /// https://www.chromium.org/updates/same-site/incompatible-clients but that cannot
        /// be shipped with the library due to the license.
        /// </summary>
        public Func<string, bool> EmitSameSiteNone { get; set; }
            = userAgent => SameSiteHelper.EmitSameSiteNone(userAgent);

        /// <summary>
        /// Notification called when the logout command is about to use the 
        /// <code>StoredRequestState</code> derived from the request's RelayState data.
        /// Return a different StoredRequestState if you would like to customize the 
        /// RelayState lookup. 
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Func<HttpRequestData, StoredRequestState>
            GetLogoutResponseState
        { get; set; } = (httpRequestData) => httpRequestData.StoredRequestState;

        /// <summary>
        /// Notification called when a command is about to construct a fully-qualified url
        /// Return a non-null Uri if you need to override this per request. Otherwise
        /// it will fall back to the normal logic that checks the request Uri 
        /// and the SPOptions.PublicOrigin setting
        /// </summary>
        public Func<HttpRequestData, Uri>
            GetPublicOrigin
        { get; set; } = (httpRequestData) => null;

        /// <summary>
        /// Notification called when single logout status is returned from IDP.
        /// Return true to indicate that your notification has handled this status. Otherwise
        /// it will fall back to the normal status processing logic.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Func<Saml2LogoutResponse, StoredRequestState, bool>
            ProcessSingleLogoutResponseStatus
        { get; set; } = (response, state) => false;

        /// <summary>
        /// Get a binding that can unbind data from the supplied request. The
        /// default is to use <see cref="Saml2Binding.Get(HttpRequestData)"/>
        /// </summary>
        public Func<HttpRequestData, Saml2Binding> GetBinding { get; set; } = Saml2Binding.Get;

        /// <summary>
        /// Notification called when the command has extracted data from
        /// request (by using <see cref="Saml2Binding.Unbind(HttpRequestData, IOptions)"/>)
        /// </summary>
        public Action<UnbindResult> MessageUnbound { get; set; } = ur => { };

        /// <summary>
        /// Notification called when the ACS command has produced a
        /// <see cref="CommandResult"/>, but before anything has been applied
        /// to the outgoing response. Set the <see cref="CommandResult.HandledResult"/>
        /// flag to suppress the library's built in apply functionality to the
        /// outgoing response.
        /// </summary>
        public Action<CommandResult, Saml2Response> AcsCommandResultCreated { get; set; }
         = (cr, r) => { };

        /// <summary>
        /// Notification called when the Logout command has produced a
        /// <see cref="CommandResult"/>, but before anything has been applied
        /// to the outgoing response. Set the <see cref="CommandResult.HandledResult"/>
        /// flag to suppress the library's built in apply functionality to the
        /// outgoing response.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Action<CommandResult> LogoutCommandResultCreated { get; set; } = cr => { };

        /// <summary>
        /// Notification called when a logout request is created to initiate single log
        /// out with an identity provider.
        /// </summary>
        public Action<Saml2LogoutRequest, ClaimsPrincipal, IdentityProvider> LogoutRequestCreated { get; set; } = (lr, user, idp) => { };

        /// <summary>
        /// Notification called when a logout request has been transformed to an XML node tree.
        /// </summary>
        public Action<Saml2LogoutRequest, XDocument, Saml2BindingType> LogoutRequestXmlCreated { get; set; } = (lr, xd, bt) => { };

        /// <summary>
        /// Notification called when a logout request has been received and processed and a Logout Response has been created.
        /// </summary>
        public Action<Saml2LogoutResponse, Saml2LogoutRequest, ClaimsPrincipal, IdentityProvider> LogoutResponseCreated { get; set; }
            = (resp, req, u, idp) => { };

        public Action<Saml2LogoutResponse, XDocument, Saml2BindingType> LogoutResponseXmlCreated { get; set; } = (lr, xd, bt) => { };

        /// <summary>
        /// Notification called when metadata has been created, but before
        /// signing. At this point the contents of the metadata can be
        /// altered before presented.
        /// </summary>
        public Action<EntityDescriptor, Saml2Urls>
            MetadataCreated
        { get; set; } = (md, urls) => { };

        /// <summary>
        /// Notification called when the Metadata command has produced a
        /// <see cref="CommandResult"/>, but before anything has been applied
        /// to the outgoing response. Set the <see cref="CommandResult.HandledResult"/>
        /// flag to suppress the library's built in apply functionality to the
        /// outgoing response.
        /// </summary>
        public Action<CommandResult> MetadataCommandResultCreated { get; set; } = cr => { };

        /// <summary>
        /// Notification called by the SignIn and Logout commands to validate a ReturnUrl that is not relative.
        /// Return true to indicate that you accept the ReturnUrl, false otherwise.
        /// Default validation do not accept any absolute URL.
        /// When false is returned, the SignIn and Logout commands will throw an <see cref="InvalidOperationException"/>.
        /// </summary>
        public Func<string, bool> ValidateAbsoluteReturnUrl { get; set; } = url => false;

        /// <summary>
        /// Notification called when getting an identity provider. Default version is to return
        /// the given idp from Options.IdentityProviders.
        /// </summary>
        public Func<EntityId, IDictionary<string, string>, IOptions, IdentityProvider> GetIdentityProvider { get; set; }
            = (ei, rd, opt) => opt.IdentityProviders[ei];

        /// <summary>
        /// Callbacks that allow modifying the validation behavior in potentially unsafe/insecure ways
        /// </summary>
        public UnsafeNotifications Unsafe { get; } = new UnsafeNotifications();

        /// <summary>
        /// Callbacks that allow modification of validation behavior in potentially unsafe/insecure ways
        /// </summary>
        internal class UnsafeNotifications
        {
            /// <summary>
            /// Notification called when the token handler has populated the
            /// <see cref="TokenValidationParameters"/>. Modify it's properties to customize
            /// the generated validation parameters.
            /// </summary>
            public Action<TokenValidationParameters, IdentityProvider, XmlElement> TokenValidationParametersCreated { get; set; } = (tvp, idp, xmlElement) => { };

            /// <summary>
            /// Notification called when an incoming Saml Response contains an unexpected
            /// InResponseTo value. Return true to acceppt the message despite this.
            /// </summary>
            /// <remarks>This notification has been added to aid in troubleshooting a
            /// hard-to-track-down issue. It will be removed in a future release if a 
            /// better solution is identified thanks to the added production analysis
            /// that this enables.</remarks>
            public Func<Saml2Response, IEnumerable<ClaimsIdentity>, bool> IgnoreUnexpectedInResponseTo { get; set; } = (r, ci) => false;
        }
    }
}
