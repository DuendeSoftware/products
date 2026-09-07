// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Xml;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Exceptions;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace Duende.IdentityServer.Internal.Saml.Sp.Commands
{
    /// <summary>
    /// Represents the assertion consumer service command behaviour.
    /// Instances of this class can be created directly or by using the factory method
    /// CommandFactory.GetCommand(CommandFactory.AcsCommandName).
    /// </summary>
    internal class AcsCommand : ICommand
    {
        /// <summary>
        /// Run the command, initiating or handling the assertion consumer sequence.
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

            var binding = options.Notifications.GetBinding(request);

            if (binding != null)
            {
                UnbindResult unbindResult = null;
                try
                {
                    unbindResult = binding.Unbind(request, options);

                    options.Notifications.MessageUnbound(unbindResult);

                    var samlResponse = new Saml2Response(unbindResult.Data, request.StoredRequestState?.MessageId, options);

                    var idpContext = GetIdpContext(unbindResult.Data, request, options);

                    var result = ProcessResponse(options, samlResponse, request.StoredRequestState, idpContext, unbindResult.RelayState);

                    if (request.StoredRequestState != null)
                    {
                        var urls = new Saml2Urls(request, options);
                        result.ClearCookieName = StoredRequestState.CookieNameBase + unbindResult.RelayState;
                        result.SetCookieSecureFlag = urls.AssertionConsumerServiceUrl.IsHttps();
                    }

                    options.Notifications.AcsCommandResultCreated(result, samlResponse);

                    return result;
                }
                catch (FormatException ex)
                {
                    throw new BadFormatSamlResponseException(
                        "The SAML Response did not contain valid BASE64 encoded data.", ex);
                }
                catch (XmlException ex)
                {
                    var newEx = new BadFormatSamlResponseException(
                        "The SAML response contains incorrect XML", ex);

                    // Add the payload to the exception
                    if (unbindResult != null)
                    {
                        newEx.Data["Saml2Response"] = unbindResult.Data.OuterXml;
                    }
                    throw newEx;
                }
                catch (Exception ex)
                {
                    if (unbindResult != null)
                    {
                        // Add the payload to the existing exception
                        ex.Data["Saml2Response"] = unbindResult.Data.OuterXml;
                    }
                    throw;
                }
            }

            throw new NoSamlResponseFoundException();
        }

        private static IdentityProvider GetIdpContext(XmlElement xml, HttpRequestData request, IOptions options)
        {
            var entityId = new EntityId(xml["Issuer", Saml2Namespaces.Saml2Name].GetTrimmedTextIfNotNull());

            var identityProvider = options.Notifications.GetIdentityProvider(entityId, request.StoredRequestState?.RelayData, options);

            return identityProvider;
        }

        private static Uri GetLocation(StoredRequestState storedRequestState, IOptions options)
        {
            // When SP-Initiated
            if (storedRequestState != null)
            {
                return storedRequestState.ReturnUrl ?? options.SPOptions.IdpInitiatedCallbackUrl;
            }

            // When IDP-Initiated, always use the configured IdpInitiatedCallbackUrl
            return options.SPOptions.IdpInitiatedCallbackUrl;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AuthenticationProperty")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "RedirectUri")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "returnUrl")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SpOptions")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ReturnUrl")]
        private static CommandResult ProcessResponse(
            IOptions options,
            Saml2Response samlResponse,
            StoredRequestState storedRequestState,
            IdentityProvider identityProvider,
            string relayState)
        {
            ValidateIssuer(storedRequestState?.Idp, samlResponse);

            var principal = new ClaimsPrincipal(samlResponse.GetClaims(options, storedRequestState?.RelayData));

            if (options.SPOptions.IdpInitiatedCallbackUrl == null)
            {
                if (storedRequestState == null)
                {
                    throw new InvalidOperationException(UnsolicitedMissingReturnUrlMessage);
                }
                if (storedRequestState.ReturnUrl == null)
                {
                    throw new InvalidOperationException(SpInitiatedMissingReturnUrl);
                }
            }

            options.SPOptions.Logger.WriteInformation("Successfully processed SAML response "
                + samlResponse.Id.Value + " and authenticated "
                + principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            return new CommandResult()
            {
                HttpStatusCode = HttpStatusCode.SeeOther,
                Location = GetLocation(storedRequestState, options),
                Principal = principal,
                RelayData = storedRequestState?.RelayData,
                SessionNotOnOrAfter = samlResponse.SessionNotOnOrAfter
            };
        }

        private static void ValidateIssuer(EntityId idp, Saml2Response samlResponse)
        {
            if (idp != null && idp.Id != samlResponse.Issuer.Id)
            {
                throw new Saml2ResponseFailedValidationException(
                    $"Unexpected issuer {samlResponse.Issuer.Id} found in response, request was sent to {idp.Id}");
            }
        }

        internal const string UnsolicitedMissingReturnUrlMessage =
@"Unsolicited SAML response received, but no IdpInitiatedCallbackUrl is configured.

When receiving unsolicited SAML responses (i.e. IDP initiated login),
the SP handler will redirect the client to the configured IdpInitiatedCallbackUrl
after successful authentication, but it is not configured.

Configure an IdpInitiatedCallbackUrl on SamlServiceProviderOptions (for standalone SP)
or on SamlProvider (for dynamic providers).";

        internal const string SpInitiatedMissingReturnUrl =
@"Successfully received and validated response from Idp, but don't know
where to redirect now. There was no return url specified when initiating
the request and there is no IdpInitiatedCallbackUrl configured.

When initiating a request, pass a ReturnUrl query parameter (case matters) or
use the RedirectUri AuthenticationProperty. Or add an IdpInitiatedCallbackUrl
in the configuration.";
    }
}
