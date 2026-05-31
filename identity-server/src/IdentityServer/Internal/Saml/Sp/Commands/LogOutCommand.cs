// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Globalization;
using System.Net;
using System.Security.Claims;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Exceptions;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;
namespace Duende.IdentityServer.Internal.Saml.Sp.Commands
{
    /// <summary>
    /// Represents the logout command behaviour.
    /// Instances of this class can be created directly or by using the factory method
    /// CommandFactory.GetCommand(CommandFactory.LogoutCommandName).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
    internal class LogoutCommand : ICommand
    {
        /// <summary>
        /// Run the command, initiating or handling the logout sequence.
        /// </summary>
        /// <param name="request">Request data.</param>
        /// <param name="options">Options</param>
        /// <param name="timeProvider">The time provider.</param>
        /// <returns>CommandResult</returns>
        public CommandResult Run(HttpRequestData request, IOptions options, TimeProvider timeProvider)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var returnUrl = request.QueryString["ReturnUrl"].SingleOrDefault();
            if (returnUrl != null && !PathHelper.IsLocalWebUrl(returnUrl))
            {
                if (!options.Notifications.ValidateAbsoluteReturnUrl(returnUrl))
                {
                    throw new InvalidOperationException("Return Url must be a relative Url.");
                }
            }

            return Run(request, returnUrl, options);
        }

        /// <summary>
        /// Run the command, initating or handling the logout sequence.
        /// </summary>
        /// <param name="request">Request data.</param>
        /// <param name="returnPath">Path to return to, only used if this
        /// is the start of an SP-initiated logout.</param>
        /// <param name="options">Options</param>
        /// <returns>CommandResult</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2234:PassSystemUriObjectsInsteadOfStrings")]
        public static CommandResult Run(
            HttpRequestData request,
            string returnPath,
            IOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            CommandResult commandResult;
            var returnUrl = GetReturnUrl(request, returnPath, options);
            var binding = options.Notifications.GetBinding(request);
            if (binding != null)
            {
                var unbindResult = binding.Unbind(request, options);
                options.Notifications.MessageUnbound(unbindResult);

                switch (unbindResult.Data.LocalName)
                {
                    case "LogoutRequest":
                        VerifyMessageIsSigned(unbindResult, options);
                        commandResult = HandleRequest(unbindResult, request, options);
                        break;
                    case "LogoutResponse":
                        if (!options.SPOptions.Compatibility.AcceptUnsignedLogoutResponses)
                        {
                            VerifyMessageIsSigned(unbindResult, options);
                        }
                        var storedRequestState = options.Notifications.GetLogoutResponseState(request);
                        var urls = new Saml2Urls(request, options);
                        commandResult = HandleResponse(unbindResult, storedRequestState, options, returnUrl, urls);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                commandResult = InitiateLogout(request, returnUrl, options, true);
            }
            options.Notifications.LogoutCommandResultCreated(commandResult);
            return commandResult;
        }

        private static void VerifyMessageIsSigned(UnbindResult unbindResult, IOptions options)
        {
            if (unbindResult.TrustLevel < TrustLevel.Signature)
            {
                var issuer = unbindResult.Data["Issuer", Saml2Namespaces.Saml2Name]?.InnerText;

                if (issuer == null)
                {
                    throw new InvalidSignatureException("There is no Issuer element in the message, so there is no way to know what certificate to use to validate the signature.");
                }
                var idp = options.IdentityProviders[new EntityId(issuer)];

                if (!unbindResult.Data.IsSignedByAny(
                    idp.SigningKeys,
                    options.SPOptions.ValidateCertificates,
                    options.SPOptions.MinIncomingSigningAlgorithm))
                {
                    throw new UnsuccessfulSamlOperationException(string.Format(CultureInfo.InvariantCulture,
                        "Received a {0} from {1} that cannot be processed because it is not signed.",
                        unbindResult.Data.LocalName,
                        unbindResult.Data["Issuer", Saml2Namespaces.Saml2Name].InnerText));
                }
            }
        }

        /// <summary>
        /// Initiatiate a federated logout.
        /// </summary>
        /// <param name="request">Request data</param>
        /// <param name="returnUrl">Return url to redirect to after logout</param>
        /// <param name="options">optins</param>
        /// <param name="terminateLocalSession">Terminate local session as part of signout?</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "signingCertificate")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SingleLogoutServiceUrl")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SPOptions")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "LogoutNameIdentifier")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "DisableOutboundLogoutRequests")]
        public static CommandResult InitiateLogout(HttpRequestData request, Uri returnUrl, IOptions options, bool terminateLocalSession)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            string idpEntityId = null;
            Claim sessionIndexClaim = null;
            if (request.User != null)
            {
                idpEntityId = request.User.FindFirst(Saml2ClaimTypes.LogoutNameIdentifier)?.Issuer;
                sessionIndexClaim = request.User.FindFirst(Saml2ClaimTypes.SessionIndex);
            }

            var knownIdp = options.IdentityProviders.TryGetValue(new EntityId(idpEntityId), out IdentityProvider idp);

            options.SPOptions.Logger.WriteVerbose("Initiating logout, checking requirements for federated logout"
                + "\n  Issuer of LogoutNameIdentifier claim (should be Idp entity id): " + idpEntityId
                + "\n  Issuer is a known Idp: " + knownIdp
                + "\n  Session index claim (should have a value): " + sessionIndexClaim
                + "\n  Idp has SingleLogoutServiceUrl: " + idp?.SingleLogoutServiceUrl?.OriginalString
                + "\n  There is a signingCertificate in SPOptions: " + (options.SPOptions.SigningServiceCertificate != null)
                + "\n  Idp configured to DisableOutboundLogoutRequests (should be false): " + idp?.DisableOutboundLogoutRequests);

            CommandResult commandResult;
            if (idpEntityId != null
                && knownIdp
                && sessionIndexClaim != null
                && idp.SingleLogoutServiceUrl != null
                && options.SPOptions.SigningServiceCertificate != null
                && !idp.DisableOutboundLogoutRequests)
            {
                var logoutRequest = idp.CreateLogoutRequest(request.User);

                options.Notifications.LogoutRequestCreated(logoutRequest, request.User, idp);

                commandResult = Saml2Binding.Get(idp.SingleLogoutServiceBinding)
                    .Bind(logoutRequest, options.SPOptions.Logger, options.Notifications.LogoutRequestXmlCreated);

                commandResult.RelayState = logoutRequest.RelayState;
                commandResult.RequestState = new StoredRequestState(
                    idp.EntityId,
                    returnUrl,
                    logoutRequest.Id,
                    null);

                if (!options.SPOptions.Compatibility.DisableLogoutStateCookie)
                {
                    commandResult.SetCookieName = StoredRequestState.CookieNameBase + logoutRequest.RelayState;

                    var urls = new Saml2Urls(request, options);
                    commandResult.SetCookieSecureFlag = urls.LogoutUrl.IsHttps();
                }

                commandResult.TerminateLocalSession = terminateLocalSession;

                options.SPOptions.Logger.WriteInformation("Sending logout request to " + idp.EntityId.Id);
            }
            else
            {
                commandResult = new CommandResult
                {
                    HttpStatusCode = HttpStatusCode.SeeOther,
                    Location = returnUrl,
                    TerminateLocalSession = terminateLocalSession
                };

                options.SPOptions.Logger.WriteInformation(
                    "Federated logout not possible, redirecting to post-logout"
                    + (terminateLocalSession ? " and clearing local session" : ""));
            }

            return commandResult;
        }

        private static Uri GetReturnUrl(HttpRequestData request, string returnPath, IOptions options)
        {
            if (!string.IsNullOrEmpty(returnPath))
            {
                return new Uri(returnPath, UriKind.RelativeOrAbsolute);
            }
            else
            {
                return new Saml2Urls(request, options).ApplicationUrl;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SingleLogoutServiceUrl")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SingleLogoutService")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "LogoutRequest")]
        private static CommandResult HandleRequest(UnbindResult unbindResult, HttpRequestData httpRequest, IOptions options)
        {
            var request = Saml2LogoutRequest.FromXml(unbindResult.Data);

            var idp = options.IdentityProviders[request.Issuer];

            if (options.SPOptions.SigningServiceCertificate == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Received a LogoutRequest from \"{0}\" but cannot reply because single logout responses " +
                    "must be signed and there is no signing certificate configured. Looks like the idp is " +
                    "configured for Single Logout despite Saml2 not exposing that functionality in the metadata.",
                    request.Issuer.Id));
            }

            if (idp.SingleLogoutServiceResponseUrl == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Received a LogoutRequest from \"{0}\" but cannot reply because on logout endpoint is " +
                    "configured on the idp. Set a SingleLogoutServiceUrl if the idp is configured manually, " +
                    "or check that the idp metadata contains a SingleLogoutService endpoint.",
                    idp.EntityId.Id));
            }

            var response = new Saml2LogoutResponse(Saml2StatusCode.Success)
            {
                DestinationUrl = idp.SingleLogoutServiceResponseUrl,
                SigningCertificate = options.SPOptions.SigningServiceCertificate,
                SigningAlgorithm = idp.OutboundSigningAlgorithm,
                InResponseTo = request.Id,
                Issuer = options.SPOptions.EntityId,
                RelayState = unbindResult.RelayState
            };

            options.Notifications.LogoutResponseCreated(response, request, httpRequest.User, idp);

            options.SPOptions.Logger.WriteInformation("Got a logout request " + request.Id
                + ", responding with logout response " + response.Id);


            // NOTE: this has been minimized to not interfere with federated logout. However,
            // we need to keep a result with TerminateLocalSession set to true in order for the
            // logout to be handled properly.
            var result = new CommandResult
            {
                TerminateLocalSession = true
            };
            return result;
        }

        private static CommandResult HandleResponse(
            UnbindResult unbindResult,
            StoredRequestState storedRequestState,
            IOptions options,
            Uri returnUrl,
            Saml2Urls urls)
        {
            var logoutResponse = Saml2LogoutResponse.FromXml(unbindResult.Data);
            var notificationHandledTheStatus = options.Notifications.ProcessSingleLogoutResponseStatus(logoutResponse, storedRequestState);
            if (!notificationHandledTheStatus)
            {
                var status = logoutResponse.Status;
                if (status != Saml2StatusCode.Success)
                {
                    throw new UnsuccessfulSamlOperationException(string.Format(CultureInfo.InvariantCulture,
                        "Idp returned status \"{0}\", indicating that the single logout failed. The local session has been successfully terminated.",
                        status));
                }
            }

            var commandResult = new CommandResult
            {
                HttpStatusCode = HttpStatusCode.SeeOther
            };
            if (!options.SPOptions.Compatibility.DisableLogoutStateCookie)
            {
                commandResult.ClearCookieName = StoredRequestState.CookieNameBase + unbindResult.RelayState;
                commandResult.SetCookieSecureFlag = urls.LogoutUrl.IsHttps();
            }
            commandResult.Location = storedRequestState?.ReturnUrl ?? returnUrl;

            options.SPOptions.Logger.WriteInformation("Received logout response " + logoutResponse.Id.Value
                + ", redirecting to " + commandResult.Location);

            return commandResult;
        }
    }
}
