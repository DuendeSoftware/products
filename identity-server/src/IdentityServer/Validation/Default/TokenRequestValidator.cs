// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Collections.Specialized;
using System.Text;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Logging.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Validation;

internal class TokenRequestValidator : ITokenRequestValidator
{
    private readonly IdentityServerOptions _options;
    private readonly IIssuerNameService _issuerNameService;
    private readonly IServerUrls _serverUrls;
    private readonly IAuthorizationCodeStore _authorizationCodeStore;
    private readonly ExtensionGrantValidator _extensionGrantValidator;
    private readonly ICustomTokenRequestValidator _customRequestValidator;
    private readonly IResourceValidator _resourceValidator;
    private readonly IResourceStore _resourceStore;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IDPoPProofValidator _dPoPProofValidator;
    private readonly IEventService _events;
    private readonly IResourceOwnerPasswordValidator _resourceOwnerValidator;
    private readonly IProfileService _profile;
    private readonly IDeviceCodeValidator _deviceCodeValidator;
    private readonly IBackchannelAuthenticationRequestIdValidator _backchannelAuthenticationRequestIdValidator;
    private readonly TimeProvider _timeProvider;
    private readonly LicenseUsageTracker _licenseUsage;
    private readonly ClientLoadedTracker _clientLoadedTracker;
    private readonly ResourceLoadedTracker _resourceLoadedTracker;
    private readonly IMtlsEndpointGenerator _mtlsEndpointGenerator;
    private readonly ILogger _logger;

    private ValidatedTokenRequest _validatedRequest;
    private CT _ct;

    public TokenRequestValidator(
        IdentityServerOptions options,
        IIssuerNameService issuerNameService,
        IServerUrls serverUrls,
        IAuthorizationCodeStore authorizationCodeStore,
        IResourceOwnerPasswordValidator resourceOwnerValidator,
        IProfileService profile,
        IDeviceCodeValidator deviceCodeValidator,
        IBackchannelAuthenticationRequestIdValidator backchannelAuthenticationRequestIdValidator,
        ExtensionGrantValidator extensionGrantValidator,
        ICustomTokenRequestValidator customRequestValidator,
        IResourceValidator resourceValidator,
        IResourceStore resourceStore,
        IRefreshTokenService refreshTokenService,
        IDPoPProofValidator dPoPProofValidator,
        IEventService events,
        TimeProvider timeProvider,
        LicenseUsageTracker licenseUsage,
        ClientLoadedTracker clientLoadedTracker,
        ResourceLoadedTracker resourceLoadedTracker,
        IMtlsEndpointGenerator mtlsEndpointGenerator,
        ILogger<TokenRequestValidator> logger)
    {
        _logger = logger;
        _licenseUsage = licenseUsage;
        _options = options;
        _issuerNameService = issuerNameService;
        _serverUrls = serverUrls;
        _timeProvider = timeProvider;
        _authorizationCodeStore = authorizationCodeStore;
        _resourceOwnerValidator = resourceOwnerValidator;
        _profile = profile;
        _deviceCodeValidator = deviceCodeValidator;
        _backchannelAuthenticationRequestIdValidator = backchannelAuthenticationRequestIdValidator;
        _extensionGrantValidator = extensionGrantValidator;
        _customRequestValidator = customRequestValidator;
        _resourceValidator = resourceValidator;
        _resourceStore = resourceStore;
        _refreshTokenService = refreshTokenService;
        _dPoPProofValidator = dPoPProofValidator;
        _events = events;
        _clientLoadedTracker = clientLoadedTracker;
        _resourceLoadedTracker = resourceLoadedTracker;
        _mtlsEndpointGenerator = mtlsEndpointGenerator;
    }

    // only here for legacy unit tests
    // maybe at some point we clean up the unit tests?
    internal Task<TokenRequestValidationResult> ValidateRequestAsync(NameValueCollection parameters, ClientSecretValidationResult clientValidationResult) => ValidateRequestAsync(new TokenRequestValidationContext
    {
        RequestParameters = parameters,
        ClientValidationResult = clientValidationResult
    }, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<TokenRequestValidationResult> ValidateRequestAsync(TokenRequestValidationContext context, CT ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenRequestValidator.ValidateRequest");

        _logger.LogDebug("Start token request validation");

        ArgumentNullException.ThrowIfNull(context);

        _ct = ct;
        var parameters = context.RequestParameters;
        var clientValidationResult = context.ClientValidationResult;

        _validatedRequest = new ValidatedTokenRequest
        {
            IssuerName = await _issuerNameService.GetCurrentAsync(),
            Raw = parameters ?? throw new ArgumentNullException(nameof(context.RequestParameters)),
            Options = _options
        };

        if (clientValidationResult == null)
        {
            throw new ArgumentNullException(nameof(context.ClientValidationResult));
        }

        _validatedRequest.SetClient(clientValidationResult.Client, clientValidationResult.Secret, clientValidationResult.Confirmation);

        /////////////////////////////////////////////
        // check client protocol type
        /////////////////////////////////////////////
        if (_validatedRequest.Client.ProtocolType != IdentityServerConstants.ProtocolTypes.OpenIdConnect)
        {
            LogError("Invalid protocol type for client",
                new
                {
                    clientId = _validatedRequest.Client.ClientId,
                    expectedProtocolType = IdentityServerConstants.ProtocolTypes.OpenIdConnect,
                    actualProtocolType = _validatedRequest.Client.ProtocolType
                });

            return Invalid(OidcConstants.TokenErrors.InvalidClient);
        }

        /////////////////////////////////////////////
        // check grant type
        /////////////////////////////////////////////
        var grantType = parameters.Get(OidcConstants.TokenRequest.GrantType);
        if (grantType.IsMissing())
        {
            LogError("Grant type is missing");
            return Invalid(OidcConstants.TokenErrors.UnsupportedGrantType);
        }

        if (grantType.Length > _options.InputLengthRestrictions.GrantType)
        {
            LogError("Grant type is too long");
            return Invalid(OidcConstants.TokenErrors.UnsupportedGrantType);
        }

        _validatedRequest.GrantType = grantType;

        //////////////////////////////////////////////////////////
        // check for resource indicator and basic formatting
        //////////////////////////////////////////////////////////
        var resourceIndicators = parameters.GetValues(OidcConstants.TokenRequest.Resource);
        if (resourceIndicators == null)
        {
            _validatedRequest.RequestedResourceIndicator = null;
        }
        else
        {
            if (resourceIndicators.Any(x => x.Length > _options.InputLengthRestrictions.ResourceIndicatorMaxLength))
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Resource indicator maximum length exceeded");
            }

            if (!resourceIndicators.AreValidResourceIndicatorFormat(_logger))
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Invalid resource indicator format");
            }

            if (resourceIndicators.Length > 1)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget,
                    "Multiple resource indicators not supported on token endpoint.");
            }

            _validatedRequest.RequestedResourceIndicator = resourceIndicators.SingleOrDefault();
        }

        //////////////////////////////////////////////////////////
        // proof token validation
        //////////////////////////////////////////////////////////
        var proofResult = await ValidateProofToken(context);
        if (proofResult.IsError)
        {
            return proofResult;
        }

        //////////////////////////////////////////////////////////
        // run specific logic for grants
        //////////////////////////////////////////////////////////

        return grantType switch
        {
            OidcConstants.GrantTypes.AuthorizationCode => await RunValidationAsync(
                ValidateAuthorizationCodeRequestAsync, parameters),
            OidcConstants.GrantTypes.ClientCredentials => await RunValidationAsync(
                ValidateClientCredentialsRequestAsync, parameters),
            OidcConstants.GrantTypes.Password => await RunValidationAsync(ValidateResourceOwnerCredentialRequestAsync,
                parameters),
            OidcConstants.GrantTypes.RefreshToken => await RunValidationAsync(ValidateRefreshTokenRequestAsync,
                parameters),
            OidcConstants.GrantTypes.DeviceCode => await RunValidationAsync(ValidateDeviceCodeRequestAsync, parameters),
            OidcConstants.GrantTypes.Ciba => await RunValidationAsync(ValidateCibaRequestRequestAsync, parameters),
            _ => await RunValidationAsync(ValidateExtensionGrantRequestAsync, parameters)
        };
    }

    private async Task<TokenRequestValidationResult> ValidateProofToken(TokenRequestValidationContext context)
    {

        // mTLS client cert processing
        //
        // If both a dpop proof and client certificate are present, we use the
        // proof token for sender-constraining the token. DPoP takes precedence
        // because that's the only sensible interpretation of the client's
        // request. DPoP can only be used for sender constraining the token,
        // but client certificates can also be used as client authentication.
        if (context.ClientCertificate != null && context.DPoPProofToken.IsMissing())
        {
            if (_options.MutualTls.AlwaysEmitConfirmationClaim && _validatedRequest.Confirmation.IsMissing())
            {
                // this would be an ephemeral client cert, so not already assigned previosuly via client authentication
                _validatedRequest.Confirmation = context.ClientCertificate.CreateThumbprintCnf();
            }

            _validatedRequest.ProofType = ProofType.ClientCertificate;
            _validatedRequest.ProofKeyThumbprint = context.ClientCertificate.GetSha256Thumbprint();
        }

        // DPoP
        if (context.DPoPProofToken.IsPresent())
        {
            _licenseUsage.FeatureUsed(LicenseFeature.DPoP);
            IdentityServerLicenseValidator.Instance.ValidateDPoP();

            if (context.DPoPProofToken.Length > _options.InputLengthRestrictions.DPoPProofToken)
            {
                LogError("DPoP proof token is too long");
                return Invalid(OidcConstants.TokenErrors.InvalidDPoPProof);
            }

            var tokenUrl = context.ClientCertificate == null ? _serverUrls.BaseUrl.EnsureTrailingSlash() + ProtocolRoutePaths.Token : _mtlsEndpointGenerator.GetMtlsEndpointPath(ProtocolRoutePaths.Token);
            var dpopContext = new DPoPProofValidatonContext
            {
                ExpirationValidationMode = _validatedRequest.Client.DPoPValidationMode,
                ClientClockSkew = _validatedRequest.Client.DPoPClockSkew,
                ProofToken = context.DPoPProofToken,
                Url = tokenUrl,
                Method = "POST",
            };
            var dpopResult = await _dPoPProofValidator.ValidateAsync(dpopContext);
            if (dpopResult.IsError)
            {
                LogError(dpopResult.ErrorDescription ?? dpopResult.Error);
                var err = Invalid(dpopResult.Error, dpopResult.ErrorDescription);
                err.DPoPNonce = dpopResult.ServerIssuedNonce;
                return err;
            }

            _validatedRequest.Confirmation = dpopResult.Confirmation;
            _validatedRequest.ProofType = ProofType.DPoP;
            _validatedRequest.ProofKeyThumbprint = dpopResult.JsonWebKeyThumbprint;
        }
        else if (_validatedRequest.Client.RequireDPoP)
        {
            LogError("Client requires DPoP and a DPoP header value was not provided.");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest, "Client requires DPoP and a DPoP header value was not provided.");
        }

        return Valid();
    }

    private async Task<TokenRequestValidationResult> RunValidationAsync(Func<NameValueCollection, Task<TokenRequestValidationResult>> validationFunc, NameValueCollection parameters)
    {
        // run standard validation
        var result = await validationFunc(parameters);
        if (result.IsError)
        {
            return result;
        }

        // run custom validation
        _logger.LogTrace("Calling into custom request validator: {type}", _customRequestValidator.GetType().FullName);

        var customValidationContext = new CustomTokenRequestValidationContext { Result = result };
        await _customRequestValidator.ValidateAsync(customValidationContext);

        if (customValidationContext.Result.IsError)
        {
            if (customValidationContext.Result.Error.IsPresent())
            {
                LogError("Custom token request validator", new { error = customValidationContext.Result.Error });
            }
            else
            {
                LogError("Custom token request validator error");
            }

            return customValidationContext.Result;
        }

        LogSuccess();

        var clientId = customValidationContext.Result.ValidatedRequest.ClientId;
        _licenseUsage.ClientUsed(clientId);
        _clientLoadedTracker.TrackClientLoaded(customValidationContext.Result.ValidatedRequest.Client);
        IdentityServerLicenseValidator.Instance.ValidateClient(clientId);
        _resourceLoadedTracker.TrackResources(customValidationContext.Result.ValidatedRequest.ValidatedResources.Resources);

        return customValidationContext.Result;
    }

    private async Task<TokenRequestValidationResult> ValidateAuthorizationCodeRequestAsync(NameValueCollection parameters)
    {
        _logger.LogDebug("Start validation of authorization code token request");

        /////////////////////////////////////////////
        // check if client is authorized for grant type
        /////////////////////////////////////////////
        if (!_validatedRequest.Client.AllowedGrantTypes.ToList().Contains(GrantType.AuthorizationCode) &&
            !_validatedRequest.Client.AllowedGrantTypes.ToList().Contains(GrantType.Hybrid))
        {
            LogError("Client not authorized for code flow");
            return Invalid(OidcConstants.TokenErrors.UnauthorizedClient);
        }

        /////////////////////////////////////////////
        // validate authorization code
        /////////////////////////////////////////////
        var code = parameters.Get(OidcConstants.TokenRequest.Code);
        if (code.IsMissing())
        {
            LogError("Authorization code is missing");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        if (code.Length > _options.InputLengthRestrictions.AuthorizationCode)
        {
            LogError("Authorization code is too long");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        _validatedRequest.AuthorizationCodeHandle = code;

        var authZcode = await _authorizationCodeStore.GetAuthorizationCodeAsync(code, _ct);
        if (authZcode == null)
        {
            LogError("Invalid authorization code", new { code });
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        /////////////////////////////////////////////
        // validate client binding
        /////////////////////////////////////////////
        if (authZcode.ClientId != _validatedRequest.Client.ClientId)
        {
            LogError("Client is trying to use a code from a different client", new { clientId = _validatedRequest.Client.ClientId, codeClient = authZcode.ClientId });
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        //////////////////////////////////////////////////////////
        // DPoP
        //////////////////////////////////////////////////////////
        if (authZcode.DPoPKeyThumbprint.IsPresent())
        {
            if (_validatedRequest.ProofType != ProofType.DPoP)
            {
                LogError("DPoP must be used on the token endpoint when a DPoP key thumbprint is used on the authorize endpoint.");
                return Invalid(OidcConstants.TokenErrors.InvalidDPoPProof, "DPoP must be used on the token endpoint when a DPoP key thumbprint is used on the authorize endpoint.");
            }

            if (_validatedRequest.ProofKeyThumbprint != authZcode.DPoPKeyThumbprint)
            {
                LogError("The DPoP proof token used on the token endpoint does not match the original used on the authorize endpoint.");
                return Invalid(OidcConstants.TokenErrors.InvalidDPoPProof, "The DPoP proof token used on the token endpoint does not match the original used on the authorize endpoint.");
            }
        }

        // remove code from store
        // todo: set to consumed in the future?
        await _authorizationCodeStore.RemoveAuthorizationCodeAsync(code, _ct);

        if (authZcode.CreationTime.HasExceeded(authZcode.Lifetime, _timeProvider.GetUtcNow().UtcDateTime))
        {
            LogError("Authorization code expired", new { code });
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        /////////////////////////////////////////////
        // populate session id
        /////////////////////////////////////////////
        if (authZcode.SessionId.IsPresent())
        {
            _validatedRequest.SessionId = authZcode.SessionId;
        }

        /////////////////////////////////////////////
        // validate code expiration
        /////////////////////////////////////////////
        if (authZcode.CreationTime.HasExceeded(_validatedRequest.Client.AuthorizationCodeLifetime, _timeProvider.GetUtcNow().UtcDateTime))
        {
            LogError("Authorization code is expired");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        _validatedRequest.AuthorizationCode = authZcode;
        _validatedRequest.Subject = authZcode.Subject;

        /////////////////////////////////////////////
        // validate redirect_uri
        /////////////////////////////////////////////
        var redirectUri = parameters.Get(OidcConstants.TokenRequest.RedirectUri);
        if (redirectUri.IsMissing())
        {
            LogError("Redirect URI is missing");
            return Invalid(OidcConstants.TokenErrors.UnauthorizedClient);
        }

        if (redirectUri.Equals(_validatedRequest.AuthorizationCode.RedirectUri, StringComparison.Ordinal) == false)
        {
            LogError("Invalid redirect_uri", new { redirectUri, expectedRedirectUri = _validatedRequest.AuthorizationCode.RedirectUri });
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        /////////////////////////////////////////////
        // validate scopes are present
        /////////////////////////////////////////////
        if (_validatedRequest.AuthorizationCode.RequestedScopes == null ||
            !_validatedRequest.AuthorizationCode.RequestedScopes.Any())
        {
            LogError("Authorization code has no associated scopes");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest);
        }

        //////////////////////////////////////////////////////////
        // resource indicator
        //////////////////////////////////////////////////////////
        if (_validatedRequest.RequestedResourceIndicator != null &&
            _validatedRequest.AuthorizationCode.RequestedResourceIndicators?.Any() == true &&
            !_validatedRequest.AuthorizationCode.RequestedResourceIndicators.Contains(_validatedRequest.RequestedResourceIndicator))
        {
            return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Resource indicator does not match any resource indicator in the original authorize request.");
        }

        //////////////////////////////////////////////////////////
        // resource and scope validation
        //////////////////////////////////////////////////////////
        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _validatedRequest.Client,
            Scopes = _validatedRequest.AuthorizationCode.RequestedScopes,
            ResourceIndicators = _validatedRequest.AuthorizationCode.RequestedResourceIndicators,
        });

        if (!validatedResources.Succeeded)
        {
            if (validatedResources.InvalidResourceIndicators.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Invalid resource indicator.");
            }
            if (validatedResources.InvalidScopes.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidScope, "Invalid scope.");
            }
        }

        var requestedIndicator = _validatedRequest.RequestedResourceIndicator;
        _licenseUsage.ResourceIndicatorUsed(requestedIndicator);
        IdentityServerLicenseValidator.Instance.ValidateResourceIndicators(requestedIndicator);
        _validatedRequest.ValidatedResources = validatedResources.FilterByResourceIndicator(requestedIndicator);

        /////////////////////////////////////////////
        // validate PKCE parameters
        /////////////////////////////////////////////
        var codeVerifier = parameters.Get(OidcConstants.TokenRequest.CodeVerifier);
        if (_validatedRequest.Client.RequirePkce || _validatedRequest.AuthorizationCode.CodeChallenge.IsPresent())
        {
            _logger.LogDebug("Client required a proof key for code exchange. Starting PKCE validation");

            var proofKeyResult = ValidateAuthorizationCodeWithProofKeyParameters(codeVerifier, _validatedRequest.AuthorizationCode);
            if (proofKeyResult.IsError)
            {
                return proofKeyResult;
            }

            _validatedRequest.CodeVerifier = codeVerifier;
        }
        else
        {
            if (codeVerifier.IsPresent())
            {
                LogError("Unexpected code_verifier: {codeVerifier}. This happens when the client is trying to use PKCE, but it is not enabled. Set RequirePkce to true.", codeVerifier);
                return Invalid(OidcConstants.TokenErrors.InvalidGrant);
            }
        }

        /////////////////////////////////////////////
        // make sure user is enabled
        /////////////////////////////////////////////
        var isActiveCtx = new IsActiveContext(_validatedRequest.AuthorizationCode.Subject, _validatedRequest.Client, IdentityServerConstants.ProfileIsActiveCallers.AuthorizationCodeValidation);
        await _profile.IsActiveAsync(isActiveCtx);

        if (isActiveCtx.IsActive == false)
        {
            LogError("User has been disabled", new { subjectId = _validatedRequest.AuthorizationCode.Subject.GetSubjectId() });
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        _logger.LogDebug("Validation of authorization code token request success");

        return Valid();
    }

    private async Task<TokenRequestValidationResult> ValidateClientCredentialsRequestAsync(NameValueCollection parameters)
    {
        _logger.LogDebug("Start client credentials token request validation");

        /////////////////////////////////////////////
        // check if client is authorized for grant type
        /////////////////////////////////////////////
        if (!_validatedRequest.Client.AllowedGrantTypes.ToList().Contains(GrantType.ClientCredentials))
        {
            LogError("Client not authorized for client credentials flow, check the AllowedGrantTypes setting", new { clientId = _validatedRequest.Client.ClientId });
            return Invalid(OidcConstants.TokenErrors.UnauthorizedClient);
        }

        /////////////////////////////////////////////
        // check if client is allowed to request scopes
        /////////////////////////////////////////////
        var scopeError = await ValidateRequestedScopesAndResourcesAsync(parameters, ignoreImplicitIdentityScopes: true, ignoreImplicitOfflineAccess: true);
        if (scopeError != null)
        {
            return Invalid(scopeError);
        }

        if (_validatedRequest.ValidatedResources.Resources.IdentityResources.Count > 0)
        {
            LogError("Client cannot request OpenID scopes in client credentials flow", new { clientId = _validatedRequest.Client.ClientId });
            return Invalid(OidcConstants.TokenErrors.InvalidScope);
        }

        if (_validatedRequest.ValidatedResources.Resources.OfflineAccess)
        {
            LogError("Client cannot request a refresh token in client credentials flow", new { clientId = _validatedRequest.Client.ClientId });
            return Invalid(OidcConstants.TokenErrors.InvalidScope);
        }

        _logger.LogDebug("{clientId} credentials token request validation success", _validatedRequest.Client.ClientId);
        return Valid();
    }

    private async Task<TokenRequestValidationResult> ValidateResourceOwnerCredentialRequestAsync(NameValueCollection parameters)
    {
        _logger.LogDebug("Start resource owner password token request validation");

        /////////////////////////////////////////////
        // check if client is authorized for grant type
        /////////////////////////////////////////////
        if (!_validatedRequest.Client.AllowedGrantTypes.Contains(GrantType.ResourceOwnerPassword))
        {
            LogError("Client not authorized for resource owner flow, check the AllowedGrantTypes setting", new { client_id = _validatedRequest.Client.ClientId });
            return Invalid(OidcConstants.TokenErrors.UnauthorizedClient);
        }

        /////////////////////////////////////////////
        // check if client is allowed to request scopes
        /////////////////////////////////////////////
        var scopeError = await ValidateRequestedScopesAndResourcesAsync(parameters);
        if (scopeError != null)
        {
            return Invalid(scopeError);
        }

        /////////////////////////////////////////////
        // check resource owner credentials
        /////////////////////////////////////////////
        var userName = parameters.Get(OidcConstants.TokenRequest.UserName);
        var password = parameters.Get(OidcConstants.TokenRequest.Password);

        if (userName.IsMissing())
        {
            LogError("Username is missing");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        if (password.IsMissing())
        {
            password = "";
        }

        if (userName.Length > _options.InputLengthRestrictions.UserName ||
            password.Length > _options.InputLengthRestrictions.Password)
        {
            LogError("Username or password too long");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        _validatedRequest.UserName = userName;


        /////////////////////////////////////////////
        // authenticate user
        /////////////////////////////////////////////
        var resourceOwnerContext = new ResourceOwnerPasswordValidationContext
        {
            UserName = userName,
            Password = password,
            Request = _validatedRequest
        };
        await _resourceOwnerValidator.ValidateAsync(resourceOwnerContext);

        if (resourceOwnerContext.Result.IsError)
        {
            // protect against bad validator implementations
            resourceOwnerContext.Result.Error ??= OidcConstants.TokenErrors.InvalidGrant;

            if (resourceOwnerContext.Result.Error == OidcConstants.TokenErrors.UnsupportedGrantType)
            {
                LogError("Resource owner password credential grant type not supported");
                await RaiseFailedResourceOwnerAuthenticationEventAsync(userName, "password grant type not supported", resourceOwnerContext.Request.Client.ClientId);

                return Invalid(OidcConstants.TokenErrors.UnsupportedGrantType, customResponse: resourceOwnerContext.Result.CustomResponse);
            }

            var errorDescription = "invalid_username_or_password";

            if (resourceOwnerContext.Result.ErrorDescription.IsPresent())
            {
                errorDescription = resourceOwnerContext.Result.ErrorDescription;
            }

            LogInformation("User authentication failed: ", errorDescription ?? resourceOwnerContext.Result.Error);
            await RaiseFailedResourceOwnerAuthenticationEventAsync(userName, errorDescription, resourceOwnerContext.Request.Client.ClientId);

            return Invalid(resourceOwnerContext.Result.Error, errorDescription, resourceOwnerContext.Result.CustomResponse);
        }

        if (resourceOwnerContext.Result.Subject == null)
        {
            var error = "User authentication failed: no principal returned";
            LogError(error);
            await RaiseFailedResourceOwnerAuthenticationEventAsync(userName, error, resourceOwnerContext.Request.Client.ClientId);

            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        /////////////////////////////////////////////
        // make sure user is enabled
        /////////////////////////////////////////////
        var isActiveCtx = new IsActiveContext(resourceOwnerContext.Result.Subject, _validatedRequest.Client, IdentityServerConstants.ProfileIsActiveCallers.ResourceOwnerValidation);
        await _profile.IsActiveAsync(isActiveCtx);

        if (isActiveCtx.IsActive == false)
        {
            LogError("User has been disabled", new { subjectId = resourceOwnerContext.Result.Subject.GetSubjectId() });
            await RaiseFailedResourceOwnerAuthenticationEventAsync(userName, "user is inactive", resourceOwnerContext.Request.Client.ClientId);

            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        _validatedRequest.UserName = userName;
        _validatedRequest.Subject = resourceOwnerContext.Result.Subject;

        await RaiseSuccessfulResourceOwnerAuthenticationEventAsync(userName, resourceOwnerContext.Result.Subject.GetSubjectId(), resourceOwnerContext.Request.Client.ClientId);
        _logger.LogDebug("Resource owner password token request validation success.");
        return Valid(resourceOwnerContext.Result.CustomResponse);
    }

    private async Task<TokenRequestValidationResult> ValidateRefreshTokenRequestAsync(NameValueCollection parameters)
    {
        _logger.LogDebug("Start validation of refresh token request");

        var refreshTokenHandle = parameters.Get(OidcConstants.TokenRequest.RefreshToken);
        if (refreshTokenHandle.IsMissing())
        {
            LogError("Refresh token is missing");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest);
        }

        if (refreshTokenHandle.Length > _options.InputLengthRestrictions.RefreshToken)
        {
            LogError("Refresh token too long");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        var result = await _refreshTokenService.ValidateRefreshTokenAsync(refreshTokenHandle, _validatedRequest.Client);

        if (result.IsError)
        {
            LogWarning("Refresh token validation failed. aborting");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        _validatedRequest.RefreshToken = result.RefreshToken;
        _validatedRequest.RefreshTokenHandle = refreshTokenHandle;
        _validatedRequest.Subject = result.RefreshToken.Subject;
        _validatedRequest.SessionId = result.RefreshToken.SessionId;

        //////////////////////////////////////////////////////////
        // proof of possession
        //////////////////////////////////////////////////////////
        var priorProofType = result.RefreshToken.ProofType ?? ProofType.None;
        ProofKeyThumbprint[] proofs = null;

        // legacy record check (pre-6.3 before ProofType was added to the RefreshToken)
        if (result.RefreshToken.ProofType == null && result.RefreshToken.ContainsCnfValues())
        {
            // we need to extract the certificate / confirmation data from the tokens
            proofs = result.RefreshToken.GetProofKeyThumbprints();
            if (proofs.Length > 0)
            {
                // many different access tokens using diff pop mechanisms. the assumption is that they are all the same
                var numberOfTypes = proofs.Select(x => x.Type).Distinct().Count();
                if (numberOfTypes > 1)
                {
                    LogError("Mixing different confirmation methods for a refresh token is not allowed.");
                    return Invalid(OidcConstants.TokenErrors.InvalidRequest, "Mixing different confirmation methods for a refresh token is not allowed.");
                }

                // we require for public clients that all proof keys have been the same.
                if (!_validatedRequest.Client.RequireClientSecret)
                {
                    var numberOfThumbprints = proofs.Select(x => x.Thumbprint).Distinct().Count();
                    if (numberOfThumbprints > 1)
                    {
                        LogError("Mixing different confirmation keys for a refresh token is not allowed.");
                        return Invalid(OidcConstants.TokenErrors.InvalidRequest, "Mixing different confirmation keys for a refresh token is not allowed.");
                    }
                }

                priorProofType = proofs.First().Type;
            }
        }

        if (priorProofType != ProofType.None && _validatedRequest.ProofType == ProofType.None)
        {
            LogError("Proof of possession was used to obtain the initial refresh token and is required for subsequent token requests.");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest, "Proof of possession was used to obtain the initial refresh token and is required for subsequent token requests.");
        }
        if (priorProofType == ProofType.None && _validatedRequest.ProofType != ProofType.None)
        {
            LogError("Proof of possession can't be used on subsequent token requests unless used when requesting the initial refresh token.");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest, "Proof of possession can't be used on subsequent token requests unless used when requesting the initial refresh token.");
        }
        if (priorProofType != _validatedRequest.ProofType)
        {
            LogError("Different proof of possession styles can't be mixed.");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest, "Different proof of possession styles can't be mixed.");
        }

        // public clients must use the same proof as last request
        // confidential clients are allowed to pass a new DPoP proof
        if (priorProofType != ProofType.None && !_validatedRequest.Client.RequireClientSecret)
        {
            proofs ??= result.RefreshToken.GetProofKeyThumbprints();
            if (proofs.Length > 0)
            {
                var thumbprint = proofs.First().Thumbprint;
                if (_validatedRequest.ProofKeyThumbprint != thumbprint)
                {
                    if (_validatedRequest.ProofType == ProofType.ClientCertificate)
                    {
                        LogError("The client certificate in the refresh token request does not match the original used.");
                        return Invalid(OidcConstants.TokenErrors.InvalidRequest, "The client certificate in the refresh token request does not match the original used.");
                    }
                    if (_validatedRequest.ProofType == ProofType.DPoP)
                    {
                        LogError("The DPoP proof token in the refresh token request does not match the original used.");
                        return Invalid(OidcConstants.TokenErrors.InvalidDPoPProof, "The DPoP proof token in the refresh token request does not match the original used.");
                    }
                }
            }
        }

        //////////////////////////////////////////////////////////
        // resource indicator
        //////////////////////////////////////////////////////////
        var resourceIndicators = _validatedRequest.RefreshToken.AuthorizedResourceIndicators;
        if (_validatedRequest.RefreshToken.AuthorizedResourceIndicators != null)
        {
            // we had an authorization request so check current requested resource against original list
            if (_validatedRequest.RequestedResourceIndicator != null &&
                !_validatedRequest.RefreshToken.AuthorizedResourceIndicators.Contains(_validatedRequest.RequestedResourceIndicator))
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Resource indicator does not match any resource indicator in the original authorize request.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(_validatedRequest.RequestedResourceIndicator))
        {
            resourceIndicators = new[] { _validatedRequest.RequestedResourceIndicator };
        }

        //////////////////////////////////////////////////////////
        // resource and scope validation
        //////////////////////////////////////////////////////////
        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _validatedRequest.Client,
            Scopes = _validatedRequest.RefreshToken.AuthorizedScopes,
            ResourceIndicators = resourceIndicators,
        });

        if (!validatedResources.Succeeded)
        {
            if (validatedResources.InvalidResourceIndicators.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Invalid resource indicator.");
            }
            if (validatedResources.InvalidScopes.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidScope, "Invalid scope.");
            }
        }

        var requestedIndicator = _validatedRequest.RequestedResourceIndicator;
        _licenseUsage.ResourceIndicatorUsed(requestedIndicator);
        IdentityServerLicenseValidator.Instance.ValidateResourceIndicators(requestedIndicator);
        _validatedRequest.ValidatedResources = validatedResources.FilterByResourceIndicator(requestedIndicator);

        _logger.LogDebug("Validation of refresh token request success");
        // todo: more logging - similar to TokenValidator before

        return Valid();
    }

    private async Task<TokenRequestValidationResult> ValidateDeviceCodeRequestAsync(NameValueCollection parameters)
    {
        _logger.LogDebug("Start validation of device code request");

        /////////////////////////////////////////////
        // resource indicator not supported for device flow
        /////////////////////////////////////////////
        if (_validatedRequest.RequestedResourceIndicator != null)
        {
            LogError("Resource indicators not supported for device flow");
            return Invalid(OidcConstants.TokenErrors.InvalidTarget);
        }

        /////////////////////////////////////////////
        // check if client is authorized for grant type
        /////////////////////////////////////////////
        if (!_validatedRequest.Client.AllowedGrantTypes.ToList().Contains(GrantType.DeviceFlow))
        {
            LogError("Client not authorized for device flow");
            return Invalid(OidcConstants.TokenErrors.UnauthorizedClient);
        }

        /////////////////////////////////////////////
        // validate device code parameter
        /////////////////////////////////////////////
        var deviceCode = parameters.Get(OidcConstants.TokenRequest.DeviceCode);
        if (deviceCode.IsMissing())
        {
            LogError("Device code is missing");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest);
        }

        if (deviceCode.Length > _options.InputLengthRestrictions.DeviceCode)
        {
            LogError("Device code too long");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        /////////////////////////////////////////////
        // validate device code
        /////////////////////////////////////////////
        var deviceCodeContext = new DeviceCodeValidationContext { DeviceCode = deviceCode, Request = _validatedRequest };
        await _deviceCodeValidator.ValidateAsync(deviceCodeContext, _ct);

        if (deviceCodeContext.Result.IsError)
        {
            return deviceCodeContext.Result;
        }

        //////////////////////////////////////////////////////////
        // scope validation
        //////////////////////////////////////////////////////////
        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _validatedRequest.Client,
            Scopes = _validatedRequest.DeviceCode.AuthorizedScopes,
            ResourceIndicators = null // not supported for device grant
        });

        if (!validatedResources.Succeeded)
        {
            if (validatedResources.InvalidResourceIndicators.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Invalid resource indicator.");
            }
            if (validatedResources.InvalidScopes.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidScope, "Invalid scope.");
            }
        }

        var requestedIndicator = _validatedRequest.RequestedResourceIndicator;
        _licenseUsage.ResourceIndicatorUsed(requestedIndicator);
        IdentityServerLicenseValidator.Instance.ValidateResourceIndicators(requestedIndicator);
        _validatedRequest.ValidatedResources = validatedResources;

        _logger.LogDebug("Validation of device code token request success");

        return Valid();
    }

    private async Task<TokenRequestValidationResult> ValidateCibaRequestRequestAsync(NameValueCollection parameters)
    {
        _logger.LogDebug("Start validation of CIBA request");

        /////////////////////////////////////////////
        // check if client is authorized for grant type
        /////////////////////////////////////////////
        if (!_validatedRequest.Client.AllowedGrantTypes.ToList().Contains(OidcConstants.GrantTypes.Ciba))
        {
            LogError("Client not authorized for CIBA flow");
            return Invalid(OidcConstants.TokenErrors.UnauthorizedClient);
        }

        _licenseUsage.FeatureUsed(LicenseFeature.CIBA);
        IdentityServerLicenseValidator.Instance.ValidateCiba();

        /////////////////////////////////////////////
        // validate authentication request id parameter
        /////////////////////////////////////////////
        var authRequestId = parameters.Get(OidcConstants.TokenRequest.AuthenticationRequestId);
        if (authRequestId.IsMissing())
        {
            LogError("Authentication request id is missing");
            return Invalid(OidcConstants.TokenErrors.InvalidRequest);
        }

        if (authRequestId.Length > _options.InputLengthRestrictions.AuthenticationRequestId)
        {
            LogError("Authentication request id too long");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        /////////////////////////////////////////////
        // validate authentication request id
        /////////////////////////////////////////////
        var validationContext = new BackchannelAuthenticationRequestIdValidationContext
        {
            AuthenticationRequestId = authRequestId,
            Request = _validatedRequest
        };
        await _backchannelAuthenticationRequestIdValidator.ValidateAsync(validationContext, _ct);

        if (validationContext.Result.IsError)
        {
            return validationContext.Result;
        }

        //////////////////////////////////////////////////////////
        // resource indicator
        //////////////////////////////////////////////////////////
        if (_validatedRequest.RequestedResourceIndicator != null &&
            _validatedRequest.BackChannelAuthenticationRequest.RequestedResourceIndicators?.Any() == true &&
            !_validatedRequest.BackChannelAuthenticationRequest.RequestedResourceIndicators.Contains(_validatedRequest.RequestedResourceIndicator))
        {
            return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Resource indicator does not match any resource indicator in the original backchannel authentication request.");
        }

        //////////////////////////////////////////////////////////
        // resource and scope validation
        //////////////////////////////////////////////////////////
        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _validatedRequest.Client,
            Scopes = _validatedRequest.BackChannelAuthenticationRequest.AuthorizedScopes,
            ResourceIndicators = _validatedRequest.BackChannelAuthenticationRequest.RequestedResourceIndicators,
        });

        if (!validatedResources.Succeeded)
        {
            if (validatedResources.InvalidResourceIndicators.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidTarget, "Invalid resource indicator.");
            }
            if (validatedResources.InvalidScopes.Count > 0)
            {
                return Invalid(OidcConstants.AuthorizeErrors.InvalidScope, "Invalid scope.");
            }
        }

        var requestedIndicator = _validatedRequest.RequestedResourceIndicator;
        _licenseUsage.ResourceIndicatorUsed(requestedIndicator);
        IdentityServerLicenseValidator.Instance.ValidateResourceIndicators(requestedIndicator);
        _validatedRequest.ValidatedResources = validatedResources.FilterByResourceIndicator(requestedIndicator);

        _logger.LogDebug("Validation of CIBA token request success");

        return Valid();
    }

    private async Task<TokenRequestValidationResult> ValidateExtensionGrantRequestAsync(NameValueCollection parameters)
    {
        _logger.LogDebug("Start validation of custom grant token request");

        /////////////////////////////////////////////
        // check if client is allowed to use grant type
        /////////////////////////////////////////////
        if (!_validatedRequest.Client.AllowedGrantTypes.Contains(_validatedRequest.GrantType))
        {
            LogError("Client does not have the custom grant type in the allowed list, therefore requested grant is not allowed", new { clientId = _validatedRequest.Client.ClientId });
            return Invalid(OidcConstants.TokenErrors.UnsupportedGrantType);
        }

        /////////////////////////////////////////////
        // check if a validator is registered for the grant type
        /////////////////////////////////////////////
        if (!_extensionGrantValidator.GetAvailableGrantTypes().Contains(_validatedRequest.GrantType, StringComparer.Ordinal))
        {
            LogError("No validator is registered for the grant type", new { grantType = _validatedRequest.GrantType });
            return Invalid(OidcConstants.TokenErrors.UnsupportedGrantType);
        }

        /////////////////////////////////////////////
        // check if client is allowed to request scopes
        /////////////////////////////////////////////
        var scopeError = await ValidateRequestedScopesAndResourcesAsync(parameters);
        if (scopeError != null)
        {
            return Invalid(scopeError);
        }

        /////////////////////////////////////////////
        // validate custom grant type
        /////////////////////////////////////////////
        var result = await _extensionGrantValidator.ValidateAsync(_validatedRequest);

        if (result == null)
        {
            LogError("Invalid extension grant");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        if (result.IsError)
        {
            if (result.Error.IsPresent())
            {
                LogError("Invalid extension grant", new { error = result.Error });
                return Invalid(result.Error, result.ErrorDescription, result.CustomResponse);
            }
            else
            {
                LogError("Invalid extension grant");
                return Invalid(OidcConstants.TokenErrors.InvalidGrant, customResponse: result.CustomResponse);
            }
        }

        if (result.Subject != null)
        {
            /////////////////////////////////////////////
            // make sure user is enabled
            /////////////////////////////////////////////
            var isActiveCtx = new IsActiveContext(
                result.Subject,
                _validatedRequest.Client,
                IdentityServerConstants.ProfileIsActiveCallers.ExtensionGrantValidation);

            await _profile.IsActiveAsync(isActiveCtx);

            if (isActiveCtx.IsActive == false)
            {
                // todo: raise event (or an OTEL metric event)?

                LogError("User has been disabled", new { subjectId = result.Subject.GetSubjectId() });
                return Invalid(OidcConstants.TokenErrors.InvalidGrant);
            }

            _validatedRequest.Subject = result.Subject;
        }

        _logger.LogDebug("Validation of extension grant token request success");
        return Valid(result.CustomResponse);
    }

    // todo: do we want to rework the semantics of these ignore params?
    // also seems like other workflows other than CC clients can omit scopes?
    private async Task<string> ValidateRequestedScopesAndResourcesAsync(NameValueCollection parameters, bool ignoreImplicitIdentityScopes = false, bool ignoreImplicitOfflineAccess = false)
    {
        var scopes = parameters.Get(OidcConstants.TokenRequest.Scope);
        if (scopes.IsMissing())
        {
            _logger.LogTrace("Client provided no scopes - checking allowed scopes list");

            if (!IEnumerableExtensions.IsNullOrEmpty(_validatedRequest.Client.AllowedScopes))
            {
                // this finds all the scopes the client is allowed to access
                var clientAllowedScopes = new List<string>();
                if (!ignoreImplicitIdentityScopes)
                {
                    var resources = await _resourceStore.FindResourcesByScopeAsync(_validatedRequest.Client.AllowedScopes);
                    clientAllowedScopes.AddRange(resources.ToScopeNames().Where(x => _validatedRequest.Client.AllowedScopes.Contains(x)));
                }
                else
                {
                    var apiScopes = await _resourceStore.FindApiScopesByNameAsync(_validatedRequest.Client.AllowedScopes);
                    clientAllowedScopes.AddRange(apiScopes.Select(x => x.Name));
                }

                if (!ignoreImplicitOfflineAccess)
                {
                    if (_validatedRequest.Client.AllowOfflineAccess)
                    {
                        clientAllowedScopes.Add(IdentityServerConstants.StandardScopes.OfflineAccess);
                    }
                }

                scopes = clientAllowedScopes.Distinct().ToSpaceSeparatedString();
                _logger.LogTrace("Defaulting to: {scopes}", scopes);
            }
            else
            {
                LogError("No allowed scopes configured for client", new { clientId = _validatedRequest.Client.ClientId });
                return OidcConstants.TokenErrors.InvalidScope;
            }
        }

        if (scopes.Length > _options.InputLengthRestrictions.Scope)
        {
            LogError("Scope parameter exceeds max allowed length");
            return OidcConstants.TokenErrors.InvalidScope;
        }

        var requestedScopes = scopes.ParseScopesString();

        if (requestedScopes == null)
        {
            LogError("No scopes found in request");
            return OidcConstants.TokenErrors.InvalidScope;
        }


        var resourceIndicators = _validatedRequest.RequestedResourceIndicator == null ?
            Enumerable.Empty<string>() :
            new[] { _validatedRequest.RequestedResourceIndicator };

        var resourceValidationResult = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _validatedRequest.Client,
            Scopes = requestedScopes,
            ResourceIndicators = resourceIndicators,
        });

        if (!resourceValidationResult.Succeeded)
        {
            if (resourceValidationResult.InvalidResourceIndicators.Count > 0)
            {
                LogError("Invalid resource indicator");
                return OidcConstants.TokenErrors.InvalidTarget;
            }

            if (resourceValidationResult.InvalidScopes.Count > 0)
            {
                LogError("Invalid scopes requested");
            }
            else
            {
                LogError("Invalid scopes for client requested");
            }

            return OidcConstants.TokenErrors.InvalidScope;
        }

        _validatedRequest.RequestedScopes = requestedScopes;

        var requestedIndicator = _validatedRequest.RequestedResourceIndicator;
        _licenseUsage.ResourceIndicatorUsed(requestedIndicator);
        IdentityServerLicenseValidator.Instance.ValidateResourceIndicators(requestedIndicator);
        _validatedRequest.ValidatedResources = resourceValidationResult.FilterByResourceIndicator(requestedIndicator);

        return null;
    }

    private TokenRequestValidationResult ValidateAuthorizationCodeWithProofKeyParameters(string codeVerifier, AuthorizationCode authZcode)
    {
        if (authZcode.CodeChallenge.IsMissing() || authZcode.CodeChallengeMethod.IsMissing())
        {
            LogError("Client is missing code challenge or code challenge method", new { clientId = _validatedRequest.Client.ClientId });
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        if (codeVerifier.IsMissing())
        {
            LogError("Missing code_verifier");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        if (codeVerifier.Length < _options.InputLengthRestrictions.CodeVerifierMinLength ||
            codeVerifier.Length > _options.InputLengthRestrictions.CodeVerifierMaxLength)
        {
            LogError("code_verifier is too short or too long");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        if (Constants.SupportedCodeChallengeMethods.Contains(authZcode.CodeChallengeMethod) == false)
        {
            LogError("Unsupported code challenge method", new { codeChallengeMethod = authZcode.CodeChallengeMethod });
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        if (ValidateCodeVerifierAgainstCodeChallenge(codeVerifier, authZcode.CodeChallenge, authZcode.CodeChallengeMethod) == false)
        {
            LogError("Transformed code verifier does not match code challenge");
            return Invalid(OidcConstants.TokenErrors.InvalidGrant);
        }

        return Valid();
    }

    private static bool ValidateCodeVerifierAgainstCodeChallenge(string codeVerifier, string codeChallenge, string codeChallengeMethod)
    {
        if (codeChallengeMethod == OidcConstants.CodeChallengeMethods.Plain)
        {
            return TimeConstantComparer.IsEqual(codeVerifier.Sha256(), codeChallenge);
        }

        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hashedBytes = codeVerifierBytes.Sha256();
        var transformedCodeVerifier = Base64Url.EncodeToString(hashedBytes);

        return TimeConstantComparer.IsEqual(transformedCodeVerifier.Sha256(), codeChallenge);
    }

    private TokenRequestValidationResult Valid(Dictionary<string, object> customResponse = null) => new TokenRequestValidationResult(_validatedRequest, customResponse);

    private TokenRequestValidationResult Invalid(string error, string errorDescription = null, Dictionary<string, object> customResponse = null) => new TokenRequestValidationResult(_validatedRequest, error, errorDescription, customResponse);

    private void LogError(string message = null, object values = null) => LogWithRequestDetails(LogLevel.Error, message, values);

    private void LogWarning(string message = null, object values = null) => LogWithRequestDetails(LogLevel.Warning, message, values);

    private void LogInformation(string message = null, object values = null) => LogWithRequestDetails(LogLevel.Information, message, values);

    private void LogWithRequestDetails(LogLevel logLevel, string message = null, object values = null)
    {
        var details = new TokenRequestValidationLog(_validatedRequest, _options.Logging.TokenRequestSensitiveValuesFilter);

        if (message.IsPresent())
        {
            try
            {
                if (values == null)
                {
                    _logger.Log(logLevel, "{Message}: {@details}", message, details);
                }
                else
                {
                    _logger.Log(logLevel, "{Message}: {@values}, details: {@details}", message, values, details);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("Error logging {exception}, request details: {@details}", ex.Message, details);
            }
        }
        else
        {
            _logger.Log(logLevel, "{@details}", details);
        }
    }

    private void LogSuccess() => LogWithRequestDetails(LogLevel.Information, "Token request validation success");

    private Task RaiseSuccessfulResourceOwnerAuthenticationEventAsync(string userName, string subjectId, string clientId)
    {
        Telemetry.Metrics.ResourceOwnerAuthentication(clientId);
        return _events.RaiseAsync(new UserLoginSuccessEvent(userName, subjectId, null, interactive: false, clientId));
    }

    private Task RaiseFailedResourceOwnerAuthenticationEventAsync(string userName, string error, string clientId)
    {
        Telemetry.Metrics.ResourceOwnerAuthenticationFailure(clientId, error);
        return _events.RaiseAsync(new UserLoginFailureEvent(userName, error, interactive: false, clientId: clientId));
    }
}
