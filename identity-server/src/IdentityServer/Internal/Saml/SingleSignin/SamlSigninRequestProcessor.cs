// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class SamlSigninRequestProcessor(
    ISamlServiceProviderStore serviceProviderStore,
    ISamlSigninInteractionResponseGenerator interactionResponseGenerator,
    ISamlSigninStateStore stateStore,
    SamlUrlBuilder samlUrlBuilder,
    TimeProvider timeProvider,
    IOptions<SamlOptions> options,
    IServerUrls serverUrls,
    SamlSigninStateIdCookie stateIdCookie,
    SamlRequestSignatureValidator<SamlSigninRequest, AuthNRequest> signatureValidator,
    SamlRequestValidator requestValidator,
    ILogger<SamlSigninRequestProcessor> logger)
    : SamlRequestProcessorBase<AuthNRequest, SamlSigninRequest, SamlSigninSuccess>(serviceProviderStore,
        options,
        requestValidator,
        signatureValidator,
        logger,
        serverUrls.GetAbsoluteUrl(options.Value.UserInteraction.Route + options.Value.UserInteraction.SignInPath))
{
    protected override async Task<Result<SamlSigninSuccess, SamlRequestError<SamlSigninRequest>>> ProcessValidatedRequestAsync(
        SamlServiceProvider sp,
        SamlSigninRequest signinRequest,
        Ct ct = default)
    {
        var authNRequest = signinRequest.AuthNRequest;

        var getAcsUrlResult = GetAcsUrl(sp, authNRequest);
        if (!getAcsUrlResult.Success)
        {
            return getAcsUrlResult.Error;
        }

        var result = await interactionResponseGenerator.ProcessInteractionAsync(sp, authNRequest, ct);

        if (result.IsError)
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Protocol,
                ProtocolError = new SamlProtocolError<SamlSigninRequest>(sp, signinRequest, result.Error!)
            };
        }

        var assertionConsumerServiceUrl = getAcsUrlResult.Value;
        switch (result.ResultType)
        {
            case SamlInteractionResponseType.Login:
                {
                    await StoreStateAsync(signinRequest, assertionConsumerServiceUrl, authNRequest, sp, ct);
                    var redirectUri = samlUrlBuilder.SamlLoginUri();

                    return SamlSigninSuccess.CreateRedirectSuccess(redirectUri);
                }
            case SamlInteractionResponseType.AlreadyAuthenticated:
                {
                    await StoreStateAsync(signinRequest, assertionConsumerServiceUrl, authNRequest, sp, ct);
                    var samlCallBackUri = samlUrlBuilder.SamlSignInCallBackUri();

                    return SamlSigninSuccess.CreateRedirectSuccess(samlCallBackUri);
                }
            case SamlInteractionResponseType.Consent:
                {
                    await StoreStateAsync(signinRequest, assertionConsumerServiceUrl, authNRequest, sp, ct);
                    var samlConsentUri = samlUrlBuilder.SamlConsentUri();

                    return SamlSigninSuccess.CreateRedirectSuccess(samlConsentUri);
                }
            case SamlInteractionResponseType.CreateAccount:
                throw new NotImplementedException("Create account isn't implemented yet");
            default:
                throw new InvalidOperationException("Unexpected result type: " + result.ResultType);
        }
    }

    private static Result<Uri, SamlRequestError<SamlSigninRequest>> GetAcsUrl(SamlServiceProvider serviceProvider,
        AuthNRequest authNRequest)
    {
        if (authNRequest.AssertionConsumerServiceUrl != null)
        {
            if (!serviceProvider.AssertionConsumerServiceUrls.Contains(authNRequest.AssertionConsumerServiceUrl))
            {
                return new SamlRequestError<SamlSigninRequest>
                {
                    Type = SamlRequestErrorType.Validation,
                    ValidationMessage =
                        $"AssertionConsumerServiceUrl '{authNRequest.AssertionConsumerServiceUrl}' is not valid"
                };
            }

            return authNRequest.AssertionConsumerServiceUrl;
        }

        if (authNRequest.AssertionConsumerServiceIndex != null)
        {
            if (authNRequest.AssertionConsumerServiceIndex.Value < 0 ||
                authNRequest.AssertionConsumerServiceIndex.Value >= serviceProvider.AssertionConsumerServiceUrls.Count)
            {
                return new SamlRequestError<SamlSigninRequest>
                {
                    Type = SamlRequestErrorType.Validation,
                    ValidationMessage =
                        $"AssertionConsumerServiceIndex '{authNRequest.AssertionConsumerServiceIndex}' is not valid"
                };
            }

            return serviceProvider.AssertionConsumerServiceUrls.ElementAt(authNRequest.AssertionConsumerServiceIndex
                .Value);
        }

        if (serviceProvider.AssertionConsumerServiceUrls.Count == 0)
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage =
                    $"The Service Provider '{serviceProvider.EntityId}' does not have any configured Assertion Consumer Service URLs"
            };
        }

        return serviceProvider.AssertionConsumerServiceUrls.First();
    }

    protected override bool RequireSignature(SamlServiceProvider sp) => sp.RequireSignedAuthnRequests;

    protected override SamlRequestError<SamlSigninRequest>? ValidateMessageSpecific(SamlServiceProvider sp, SamlSigninRequest signinRequest)
    {
        var authNRequest = signinRequest.AuthNRequest;

        // AuthNRequest-specific validation (NameIdPolicy)
        if (authNRequest.NameIdPolicy?.Format != null)
        {
            var requestedFormat = authNRequest.NameIdPolicy.Format;
            var supportedFormats = SamlOptions.SupportedNameIdFormats;

            if (!supportedFormats.Contains(requestedFormat))
            {
                Logger.RequestedNameIdFormatNotSupported(LogLevel.Debug, requestedFormat);

                var samlError = new SamlError
                {
                    StatusCode = SamlStatusCodes.Responder,
                    SubStatusCode = SamlStatusCodes.InvalidNameIdPolicy,
                    Message = $"Requested NameID format '{requestedFormat}' is not supported by this IdP"
                };
                return new SamlRequestError<SamlSigninRequest>
                {
                    Type = SamlRequestErrorType.Protocol,
                    ProtocolError = new SamlProtocolError<SamlSigninRequest>(sp, signinRequest, samlError)
                };
            }

            Logger.NameIdPolicyParsed(LogLevel.Debug, authNRequest.NameIdPolicy.Format, authNRequest.NameIdPolicy.SPNameQualifier);
        }

        return null;
    }

    private async Task StoreStateAsync(
        SamlSigninRequest signinRequest,
        Uri assertionConsumerServiceUrl,
        AuthNRequest authNRequest,
        SamlServiceProvider sp,
        Ct ct = default)
    {
        var state = new SamlAuthenticationState
        {
            Request = authNRequest,
            ServiceProviderEntityId = sp.EntityId,
            RelayState = signinRequest.RelayState,
            IsIdpInitiated = false,
            CreatedUtc = timeProvider.GetUtcNow(),
            AssertionConsumerServiceUrl = assertionConsumerServiceUrl
        };

        var stateId = await stateStore.StoreSigninRequestStateAsync(state, ct);

        stateIdCookie.StoreSamlSigninStateId(stateId);
    }
}
