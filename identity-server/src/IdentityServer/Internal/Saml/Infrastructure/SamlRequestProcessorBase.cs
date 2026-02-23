// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal abstract class SamlRequestProcessorBase<TMessage, TRequest, TSuccess>(
    ISamlServiceProviderStore serviceProviderStore,
    IOptions<SamlOptions> options,
    SamlRequestValidator requestValidator,
    SamlRequestSignatureValidator<TRequest, TMessage> signatureValidator,
    ILogger logger,
    string expectedDestination)
    where TMessage : ISamlRequest
    where TRequest : SamlRequestBase<TMessage>
{
    protected readonly ISamlServiceProviderStore ServiceProviderStore = serviceProviderStore;
    protected readonly SamlOptions SamlOptions = options.Value;
    protected readonly SamlRequestValidator RequestValidator = requestValidator;
    protected readonly SamlRequestSignatureValidator<TRequest, TMessage> SignatureValidator = signatureValidator;
    protected readonly ILogger Logger = logger;
    protected readonly string ExpectedDestination = expectedDestination;

    internal async Task<Result<TSuccess, SamlRequestError<TRequest>>> ProcessAsync(TRequest request, CT ct = default)
    {
        var sp = await ServiceProviderStore.FindByEntityIdAsync(request.Request.Issuer);
        if (sp?.Enabled != true)
        {
            Logger.ServiceProviderNotFound(LogLevel.Warning, request.Request.Issuer);
            return new SamlRequestError<TRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"Service Provider '{request.Request.Issuer}' is not registered or is disabled"
            };
        }

        var validationError = ValidateRequest(sp, request);
        if (validationError != null)
        {
            return validationError;
        }

        return await ProcessValidatedRequestAsync(sp, request, ct);
    }

    private SamlRequestError<TRequest>? ValidateRequest(SamlServiceProvider sp, TRequest request)
    {
        // Common validation (version, issue instant, destination)
        var validationError = RequestValidator.ValidateCommonFields(
            request.Request.Version,
            request.Request.IssueInstant,
            request.Request.Destination,
            sp,
            ExpectedDestination);

        if (validationError != null)
        {
            return new SamlRequestError<TRequest>
            {
                Type = SamlRequestErrorType.Protocol,
                ProtocolError = new SamlProtocolError<TRequest>(sp, request, new SamlError
                {
                    StatusCode = validationError.StatusCode,
                    SubStatusCode = validationError.SubStatusCode,
                    Message = validationError.Message
                })
            };
        }

        // Signature validation
        var signatureError = ValidateSignature(sp, request);
        if (signatureError != null)
        {
            return signatureError;
        }

        // Message-specific validation
        return ValidateMessageSpecific(sp, request);
    }

    protected abstract bool RequireSignature(SamlServiceProvider sp);

    private SamlRequestError<TRequest>? ValidateSignature(SamlServiceProvider sp, TRequest request)
    {
        var requireSignature = RequireSignature(sp);

        if (!requireSignature)
        {
            return null;
        }

        if (sp.SigningCertificates == null || sp.SigningCertificates.Count == 0)
        {
            return new SamlRequestError<TRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"Service Provider '{sp.EntityId}' has no signing certificates configured and has sent a {TMessage.MessageName} which requires signature validation"
            };
        }

        Result<bool, SamlError> validationResult;

        if (request.Binding == SamlBinding.HttpRedirect)
        {
            validationResult = SignatureValidator.ValidateRedirectBindingSignature(request, sp);
        }
        else if (request.Binding == SamlBinding.HttpPost)
        {
            validationResult = SignatureValidator.ValidatePostBindingSignature(request, sp);
        }
        else
        {
            return new SamlRequestError<TRequest>
            {
                Type = SamlRequestErrorType.Protocol,
                ProtocolError = new SamlProtocolError<TRequest>(sp, request, new SamlError
                {
                    StatusCode = SamlStatusCodes.Requester,
                    Message = $"Unsupported binding for signature validation: {request.Binding}"
                })
            };
        }

        if (!validationResult.Success)
        {
            return new SamlRequestError<TRequest>
            {
                Type = SamlRequestErrorType.Protocol,
                ProtocolError = new SamlProtocolError<TRequest>(sp, request, validationResult.Error)
            };
        }

        return null;
    }
    protected abstract SamlRequestError<TRequest>? ValidateMessageSpecific(SamlServiceProvider sp, TRequest request);
    protected abstract Task<Result<TSuccess, SamlRequestError<TRequest>>> ProcessValidatedRequestAsync(SamlServiceProvider sp, TRequest request, CT ct = default);
}
