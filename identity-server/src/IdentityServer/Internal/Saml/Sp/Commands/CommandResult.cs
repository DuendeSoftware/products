// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
namespace Duende.IdentityServer.Internal.Saml.Sp.Commands
{
    /// <summary>
    /// The results of a command.
    /// </summary>
    internal class CommandResult
    {
        /// <summary>
        /// Status code that should be returned.
        /// </summary>
        public HttpStatusCode HttpStatusCode { get; set; }

        /// <summary>
        /// Cacheability of the command result.
        /// </summary>
        public Cacheability Cacheability { get; set; }

        /// <summary>
        /// Location, if the status code is a redirect.
        /// </summary>
        public Uri Location { get; set; }

        /// <summary>
        /// The extracted principal if the command has parsed an incoming assertion.
        /// </summary>
        public ClaimsPrincipal Principal { get; set; }

        /// <summary>
        /// Session termination time. Priority order: 1. SessionNotOnOrAfter in
        /// assertion. 2. WIF configured lifetime with SessionSecurityTokenHandler
        /// 3. SessionSecurityTokenHandler default.
        /// </summary>
        public DateTime? SessionNotOnOrAfter { get; set; }

        /// <summary>
        /// The response body that is the result of the command.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// The Mime-type
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Data relayed from a previous request, such as the dictionary storing
        /// the Owin Authentication Properties.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "The RelayData is a complete piece of data, not a collection that is manipulated")]
        public IDictionary<string, string> RelayData { get; set; }

        /// <summary>
        /// Indicates that the local session should be terminated. Used by
        /// logout functionality.
        /// </summary>
        public bool TerminateLocalSession { get; set; }

        /// <summary>
        /// Name of cookie to set.
        /// </summary>
        public string SetCookieName { get; set; }

        /// <summary>
        /// Value of the "Secure" flag for the cookie (relevant if <see cref="SetCookieName"/> != null).
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flag")]
        public bool SetCookieSecureFlag { get; set; }

        /// <summary>
        /// SAML RelayState value
        /// </summary>
        public string RelayState { get; set; }

        /// <summary>
        /// Request state to store so that it is available on next http request.
        /// </summary>
        public StoredRequestState RequestState { get; set; }

        /// <summary>
        /// Serialized request state.
        /// </summary>
        public byte[] GetSerializedRequestState()
        {
            return RequestState?.Serialize();
        }

        /// <summary>
        /// Name of cookie to be cleared.
        /// </summary>
        public string ClearCookieName { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        public CommandResult()
        {
            HttpStatusCode = HttpStatusCode.OK;
            Cacheability = Cacheability.NoCache;
        }

        /// <summary>
        /// Can be set by a notification callback to indicate that the
        /// <see cref="CommandResult"/> has been handled and should not
        /// be applied by the Saml2 library to the response.
        /// </summary>
        public bool HandledResult { get; set; }

        /// <summary>
        /// Other headers that should be set on the response.
        /// </summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
    }
}
