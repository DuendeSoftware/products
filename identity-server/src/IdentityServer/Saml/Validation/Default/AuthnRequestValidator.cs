// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// AuthnRequest validator
/// </summary>
public class AuthnRequestValidator(
    ISamlServiceProviderStore serviceProviderStore,
    ISamlResourceResolver resourceResolver,
    TimeProvider timeProvider,
    IOptions<IdentityServerOptions> identityServerOptions,
    IServerUrls serverUrls,
    ILogger<AuthnRequestValidator> logger)
    : IAuthnRequestValidator
{
    /// <inheritdoc />
    public async Task<AuthnRequestValidationResult> ValidateAsync(ValidatedAuthnRequest request, Ct ct)
    {
        var spResult = await ValidateSpAsync(request, ct);
        if (spResult.IsError)
        {
            return spResult;
        }

        var signatureTrustResult = ValidateSignatureTrust(request);
        if (signatureTrustResult.IsError)
        {
            return signatureTrustResult;
        }

        var versionResult = ValidateVersion(request);
        if (versionResult.IsError)
        {
            return versionResult;
        }

        var issueInstantResult = ValidateIssueInstant(request);
        if (issueInstantResult.IsError)
        {
            return issueInstantResult;
        }

        var destinationResult = ValidateDestination(request);
        if (destinationResult.IsError)
        {
            return destinationResult;
        }

        var acsResult = ValidateAcsUrl(request);
        if (acsResult.IsError)
        {
            return acsResult;
        }

        var nameIdFormatResult = ValidateNameIdFormat(request);
        if (nameIdFormatResult.IsError)
        {
            return nameIdFormatResult;
        }

        var scopingResult = ValidateScoping(request);
        if (scopingResult.IsError)
        {
            return scopingResult;
        }

        var resourceResult = await ValidateResourcesAsync(request, ct);
        if (resourceResult.IsError)
        {
            return resourceResult;
        }

        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validate the SP/Client.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Validation result</returns>
    protected virtual async Task<AuthnRequestValidationResult> ValidateSpAsync(ValidatedAuthnRequest request, Ct ct)
    {
        if (request.AuthnRequest.Issuer == null)
        {
            return AuthnRequestValidationResult.InValid(request, SamlStatusCodes.Requester, "Missing SP EntityID in AuthnRequest");
        }

        var spEntityId = request.AuthnRequest.Issuer.Value;

        var serviceProvider = await serviceProviderStore.FindByEntityIdAsync(spEntityId, ct);

        if (serviceProvider is not { Enabled: true })
        {
            return AuthnRequestValidationResult.InValid(request, SamlStatusCodes.Requester, "Invalid SP EntityId.");
        }

        if (serviceProvider.AssertionConsumerServiceUrls.Count == 0)
        {
            return AuthnRequestValidationResult.InValid(request, SamlStatusCodes.Responder, "No Assertion Consumer Service URLs found.");
        }

        request.Saml2Sp = serviceProvider;

        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the AuthnRequest meets the SP's signature trust requirements.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <returns>Validation result</returns>
    protected virtual AuthnRequestValidationResult ValidateSignatureTrust(ValidatedAuthnRequest request)
    {
        var requireSigned = request.Saml2Sp!.RequireSignedAuthnRequests
                            ?? identityServerOptions.Value.Saml.WantAuthnRequestsSigned;

        if (!requireSigned)
        {
            return AuthnRequestValidationResult.Valid(request);
        }

        if (request.AuthnRequest.HasTrustedSignature)
        {
            return AuthnRequestValidationResult.Valid(request);
        }

        logger.AuthnRequestSignatureTrustCheckFailed(LogLevel.Warning, request.Saml2Sp.EntityId, request.AuthnRequest.TrustLevel);

        return AuthnRequestValidationResult.InValid(
            request,
            SamlStatusCodes.Requester,
            "The AuthnRequest signature is missing or not trusted");
    }

    /// <summary>
    /// Validates that the AuthnRequest uses SAML version 2.0.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <returns>Validation result</returns>
    protected virtual AuthnRequestValidationResult ValidateVersion(ValidatedAuthnRequest request)
    {
        if (request.AuthnRequest.Version != SamlVersions.V2)
        {
            return AuthnRequestValidationResult.InValid(
                request,
                SamlStatusCodes.VersionMismatch,
                "Only Version 2.0 is supported");
        }

        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the AuthnRequest IssueInstant is not in the future
    /// (beyond clock skew) and not expired (beyond max age).
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <returns>Validation result</returns>
    protected virtual AuthnRequestValidationResult ValidateIssueInstant(ValidatedAuthnRequest request)
    {
        var options = identityServerOptions.Value.Saml;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var issueInstant = request.AuthnRequest.IssueInstant;

        var clockSkew = request.Saml2Sp?.ClockSkew ?? options.DefaultClockSkew;
        if (issueInstant > now.Add(clockSkew))
        {
            return AuthnRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "Request IssueInstant is in the future");
        }

        var maxAge = request.Saml2Sp?.RequestMaxAge ?? options.DefaultRequestMaxAge;
        if (issueInstant < now.Subtract(maxAge))
        {
            return AuthnRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "Request has expired (IssueInstant too old)");
        }

        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the AuthnRequest Destination matches the expected SSO endpoint URL.
    /// Signed requests must include a Destination per SAML Bindings 2.0 §3.4.5.2/§3.5.5.2.
    /// Unsigned requests may omit the Destination.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <returns>Validation result</returns>
    protected virtual AuthnRequestValidationResult ValidateDestination(ValidatedAuthnRequest request)
    {
        var destination = request.AuthnRequest.Destination;

        if (string.IsNullOrEmpty(destination))
        {
            if (request.AuthnRequest.HasTrustedSignature)
            {
                return AuthnRequestValidationResult.InValid(
                    request,
                    SamlStatusCodes.Requester,
                    "Signed AuthnRequests must include a Destination");
            }

            return AuthnRequestValidationResult.Valid(request);
        }

        var expectedDestination = serverUrls.BaseUrl + identityServerOptions.Value.Saml.Endpoints.SingleSignOnServicePath;

        if (!destination.Equals(expectedDestination, StringComparison.OrdinalIgnoreCase))
        {
            logger.AuthnRequestDestinationMismatch(LogLevel.Warning, expectedDestination, destination);

            return AuthnRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "Invalid destination");
        }

        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates and resolves the Assertion Consumer Service endpoint for the SAML response.
    /// When the request specifies an ACS URL, all registered endpoints at that location are
    /// considered. If a ProtocolBinding is also specified, it is used to disambiguate; otherwise
    /// the default or first matching endpoint is selected. If an ACS index is specified, it must
    /// match a registered endpoint. If neither is specified, the default ACS endpoint is used.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <returns>Validation result</returns>
    protected virtual AuthnRequestValidationResult ValidateAcsUrl(ValidatedAuthnRequest request)
    {
        var sp = request.Saml2Sp!;
        var acsUrl = request.AuthnRequest.AssertionConsumerServiceUrl;
        var acsIndex = request.AuthnRequest.AssertionConsumerServiceIndex;

        if (!string.IsNullOrEmpty(acsUrl))
        {
            if (acsIndex != null)
            {
                return AuthnRequestValidationResult.InValid(
                    request,
                    SamlStatusCodes.Requester,
                    "Both ACS Url and Index were provided in the request");
            }

            if (!Uri.TryCreate(acsUrl, UriKind.Absolute, out var parsedUri))
            {
                return AuthnRequestValidationResult.InValid(
                    request,
                    SamlStatusCodes.Requester,
                    "AssertionConsumerServiceUrl is not a valid absolute URI");
            }

            var candidates = sp.AssertionConsumerServiceUrls
                .Where(acs => acs.Location == parsedUri.AbsoluteUri)
                .ToList();

            if (candidates.Count == 0)
            {
                logger.AssertionConsumerServiceUrlNotRegistered(LogLevel.Warning, sp.EntityId, acsUrl);

                return AuthnRequestValidationResult.InValid(
                    request,
                    SamlStatusCodes.Requester,
                    "AssertionConsumerServiceUrl is not registered for this Service Provider");
            }

            if (!string.IsNullOrEmpty(request.AuthnRequest.ProtocolBinding))
            {
                var bindingMatch = candidates.FirstOrDefault(
                    acs => acs.Binding.ToUrn() == request.AuthnRequest.ProtocolBinding);
                if (bindingMatch != null)
                {
                    candidates = [bindingMatch];
                }
            }

            request.AssertionConsumerService = candidates.Count == 1
                ? candidates[0]
                : candidates.FirstOrDefault(acs => acs.IsDefault) ?? candidates[0];
            return AuthnRequestValidationResult.Valid(request);
        }

        if (acsIndex != null)
        {
            var resolvedAcs = sp.AssertionConsumerServiceUrls.SingleOrDefault(acs => acs.Index == acsIndex);
            if (resolvedAcs == null)
            {
                return AuthnRequestValidationResult.InValid(
                    request,
                    SamlStatusCodes.Requester,
                    "No AssertionConsumerServiceUrl registered for this Service Provider with the provided index");
            }

            request.AssertionConsumerService = resolvedAcs;
            return AuthnRequestValidationResult.Valid(request);
        }

        request.AssertionConsumerService = sp.AssertionConsumerServiceUrls.FirstOrDefault(acs => acs.IsDefault) ?? sp.AssertionConsumerServiceUrls.First();
        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the requested NameID format is supported by this IdP.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <returns>Validation result</returns>
    protected virtual AuthnRequestValidationResult ValidateNameIdFormat(ValidatedAuthnRequest request)
    {
        var format = request.AuthnRequest.NameIdPolicy?.Format
                     ?? request.Saml2Sp?.DefaultNameIdFormat;

        if (format == null)
        {
            return AuthnRequestValidationResult.Valid(request);
        }

        if (!identityServerOptions.Value.Saml.SupportedNameIdFormats.Contains(format))
        {
            logger.AuthnRequestUnsupportedNameIdFormat(LogLevel.Warning, request.Saml2Sp?.EntityId, format);

            return AuthnRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                $"Requested NameID format '{format}' is not supported by this IdP");
        }

        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the AuthnRequest does not contain a Scoping element.
    /// Scoping is not currently supported. Override this method to allow or
    /// implement custom scoping behavior.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <returns>Validation result</returns>
    protected virtual AuthnRequestValidationResult ValidateScoping(ValidatedAuthnRequest request)
    {
        if (request.AuthnRequest.Scoping != null)
        {
            logger.AuthnRequestContainsScopingElement(LogLevel.Warning, request.Saml2Sp?.EntityId);

            return AuthnRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "Scoping is not supported");
        }

        return AuthnRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the service provider's AllowedScopes resolve to valid identity resources,
    /// that RequestedClaimTypes are within the allowed claim types, and populates
    /// <see cref="ValidatedAuthnRequest.ValidatedResources"/> with the result.
    /// </summary>
    /// <param name="request">AuthnRequest validation context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Validation result</returns>
    /// <remarks>
    /// SAML service providers must configure AllowedScopes to declare which identity resources
    /// (and their associated claim types) the SP is allowed to receive. This is the authorization
    /// ceiling — analogous to AllowedScopes on an OIDC Client.
    ///
    /// RequestedClaimTypes narrows which claim types are included in assertions. Each entry must
    /// resolve to a claim type defined by one of the identity resources in AllowedScopes.
    ///
    /// This method sets <see cref="ValidatedAuthnRequest.ValidatedResources"/> and
    /// <see cref="ValidatedAuthnRequest.RequestedClaimTypes"/> on the <paramref name="request"/>.
    /// Overriders must ensure both properties are populated on success.
    /// </remarks>
    protected virtual async Task<AuthnRequestValidationResult> ValidateResourcesAsync(ValidatedAuthnRequest request, Ct ct)
    {
        var sp = request.Saml2Sp!;

        var result = await resourceResolver.ResolveRequestedClaimTypesAsync(sp, ct);

        if (!result.Succeeded)
        {
            return AuthnRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Responder,
                result.Error);
        }

        request.ValidatedResources = result.ValidatedResources;
        request.RequestedClaimTypes = result.ClaimTypes;

        return AuthnRequestValidationResult.Valid(request);
    }
}
