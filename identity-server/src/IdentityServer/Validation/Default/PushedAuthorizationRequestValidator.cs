// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default validator for pushed authorization requests. This validator performs
/// checks that are specific to pushed authorization and also invokes the <see
/// cref="IAuthorizeRequestValidator"/> to validate the pushed parameters as if
/// they had been sent to the authorize endpoint directly.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see
/// cref="PushedAuthorizationRequestValidator"/> class.
/// </remarks>
/// <param name="authorizeRequestValidator">The authorize request validator,
/// used to validate the pushed authorization parameters as if they were
/// used directly at the authorize endpoint.</param>
/// <param name="dpopProofValidator">The dpop proof validator, used to
/// validate DPoP proofs that are sent to bind authorization codes
/// to a proof key.</param>
/// <param name="serverUrls">The server urls service</param>
/// <param name="licenseUsage">The feature manager</param>
/// <param name="options">The IdentityServer Options</param>
/// <param name="mtlsEndpointGenerator">The mTLS endpoint generator</param>
/// <param name="logger">The logger</param>
internal class PushedAuthorizationRequestValidator(
    IAuthorizeRequestValidator authorizeRequestValidator,
    IDPoPProofValidator dpopProofValidator,
    IServerUrls serverUrls,
    LicenseUsageTracker licenseUsage,
    IdentityServerOptions options,
    IMtlsEndpointGenerator mtlsEndpointGenerator,
    ILogger<PushedAuthorizationRequestValidator> logger) : IPushedAuthorizationRequestValidator
{
    public async Task<PushedAuthorizationValidationResult> ValidateAsync(PushedAuthorizationRequestValidationContext context, Ct ct)
    {
        // Licensing
        licenseUsage.FeatureUsed(LicenseFeature.PAR);
        IdentityServerLicenseValidator.Instance.ValidatePar();

        // -- Request URI validation --
        var validatedRequest = await ValidateRequestUriAsync(context);
        if (validatedRequest.IsError)
        {
            return validatedRequest;
        }

        // -- DPoP Header Validation --
        // The client can send the public key of its DPoP proof key to us. We
        // then bind its authorization code to the proof key and check for a
        // proof token signed with the key at the token endpoint.
        //
        // There are two ways for the client to send its DPoP proof key public
        // key material to us:
        // 1. pass the dpop_jkt parameter with a JWK thumbprint (RFC 7638)
        // 2. send a DPoP proof (which contains the public key as a JWK) in the
        //    DPoP http header
        //
        // If a proof is passed, then we validate it, compute the thumbprint of
        // the key within, and treat that as if it were passed as the dpop_jkt
        // parameter.
        //
        // If a proof and a dpop_jkt are both passed, its an error if they don't
        // agree.
        if (context.DPoPProofToken.IsPresent())
        {
            // bail out if unreasonably large
            if (context.DPoPProofToken.Length > options.InputLengthRestrictions.DPoPProofToken)
            {
                logger.LogError("DPoP proof token is too long");
                return new PushedAuthorizationValidationResult(
                    "invalid_dpop_proof",
                    "DPoP proof token is too long");
            }

            // validate proof token
            var parUrl = context.ClientCertificate == null ? serverUrls.BaseUrl.EnsureTrailingSlash() + ProtocolRoutePaths.PushedAuthorization : mtlsEndpointGenerator.GetMtlsEndpointPath(ProtocolRoutePaths.PushedAuthorization);
            var dpopContext = new DPoPProofValidatonContext
            {
                ProofToken = context.DPoPProofToken,
                ExpirationValidationMode = context.Client.DPoPValidationMode,
                ClientClockSkew = context.Client.DPoPClockSkew,
                ValidateAccessToken = false,
                Method = "POST",
                Url = parUrl
            };
            var dpopValidationResult = await dpopProofValidator.ValidateAsync(dpopContext, ct);
            if (dpopValidationResult.ServerIssuedNonce != null)
            {
                return PushedAuthorizationValidationResult.CreateServerNonceResult(dpopValidationResult.ServerIssuedNonce);
            }

            if (dpopValidationResult.Error != null)
            {
                return new PushedAuthorizationValidationResult(
                    dpopValidationResult.Error,
                    dpopValidationResult.ErrorDescription ?? "Invalid DPoP Proof");
            }

            // if dpop_jkt was also passed, make sure they are consistent
            var dpopThumbprintParameter = context.RequestParameters.Get(OidcConstants.AuthorizeRequest.DPoPKeyThumbprint);
            if (dpopThumbprintParameter != null)
            {
                if (dpopThumbprintParameter != dpopValidationResult.JsonWebKeyThumbprint)
                {
                    return new PushedAuthorizationValidationResult(
                        OidcConstants.AuthorizeErrors.InvalidRequest,
                        "Mismatch between thumbprint of JWK in DPoP HTTP header and dpop_jkt parameter");
                }
                // dpop_jkt and dpop header match, and the request parameters already include dpop_jkt,
                // so the code will be bound to the client's key without us doing anything more.
            }
            else
            {
                // Since dpop_jkt wasn't passed, copy the thumbprint we derived from the proof token
                // into the request parameters so that the auth code will be bound to it.
                context.RequestParameters.Add(OidcConstants.AuthorizeRequest.DPoPKeyThumbprint, dpopValidationResult.JsonWebKeyThumbprint);
            }
        }

        // -- Authorization Parameter Validation --
        var authorizeRequestValidation = await authorizeRequestValidator.ValidateAsync(context.RequestParameters, ct,
            authorizeRequestType: AuthorizeRequestType.PushedAuthorization);
        if (authorizeRequestValidation.IsError)
        {
            return new PushedAuthorizationValidationResult(
                authorizeRequestValidation.Error,
                authorizeRequestValidation.ErrorDescription,
                authorizeRequestValidation.ValidatedRequest);
        }

        return validatedRequest;
    }

    /// <summary>
    /// Validates a PAR request to ensure that it does not contain a request
    /// URI, which is explicitly disallowed by RFC 9126.
    /// </summary>
    /// <param name="context">The pushed authorization validation
    /// context.</param>
    /// <returns>A task containing the <see
    /// cref="PushedAuthorizationValidationResult"/>.</returns>
    private static Task<PushedAuthorizationValidationResult> ValidateRequestUriAsync(PushedAuthorizationRequestValidationContext context)
    {
        // Reject request_uri parameter
        if (context.RequestParameters.Get(OidcConstants.AuthorizeRequest.RequestUri).IsPresent())
        {
            return Task.FromResult(new PushedAuthorizationValidationResult("invalid_request", "Pushed authorization cannot use request_uri"));
        }
        else
        {
            return Task.FromResult(new PushedAuthorizationValidationResult(
                new ValidatedPushedAuthorizationRequest
                {
                    Raw = context.RequestParameters,
                    Client = context.Client
                }));
        }
    }
}
