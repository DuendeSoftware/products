// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// The default token response generator
/// </summary>
/// <seealso cref="ITokenResponseGenerator" />
public class TokenResponseGenerator : ITokenResponseGenerator
{
    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The token service
    /// </summary>
    protected readonly ITokenService TokenService;

    /// <summary>
    /// The refresh token service
    /// </summary>
    protected readonly IRefreshTokenService RefreshTokenService;

    /// <summary>
    /// The scope parser
    /// </summary>
    public IScopeParser ScopeParser { get; }

    /// <summary>
    /// The resource store
    /// </summary>
    protected readonly IResourceStore Resources;

    /// <summary>
    /// The clients store
    /// </summary>
    protected readonly IClientStore Clients;

    /// <summary>
    ///  The clock
    /// </summary>
    protected readonly IClock Clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenResponseGenerator" /> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="tokenService">The token service.</param>
    /// <param name="refreshTokenService">The refresh token service.</param>
    /// <param name="scopeParser">The scope parser.</param>
    /// <param name="resources">The resources.</param>
    /// <param name="clients">The clients.</param>
    /// <param name="logger">The logger.</param>
    public TokenResponseGenerator(IClock clock, ITokenService tokenService, IRefreshTokenService refreshTokenService, IScopeParser scopeParser, IResourceStore resources, IClientStore clients, ILogger<TokenResponseGenerator> logger)
    {
        Clock = clock;
        TokenService = tokenService;
        RefreshTokenService = refreshTokenService;
        ScopeParser = scopeParser;
        Resources = resources;
        Clients = clients;
        Logger = logger;
    }

    /// <summary>
    /// Processes the response.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    public virtual async Task<TokenResponse> ProcessAsync(TokenRequestValidationResult request)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("TokenResponseGenerator.Process");
        activity?.SetTag(Tracing.Properties.GrantType, request.ValidatedRequest.GrantType);
        activity?.SetTag(Tracing.Properties.ClientId, request.ValidatedRequest.Client.ClientId);

        switch (request.ValidatedRequest.GrantType)
        {
            case OidcConstants.GrantTypes.ClientCredentials:
                return await ProcessClientCredentialsRequestAsync(request);
            case OidcConstants.GrantTypes.Password:
                return await ProcessPasswordRequestAsync(request);
            case OidcConstants.GrantTypes.AuthorizationCode:
                return await ProcessAuthorizationCodeRequestAsync(request);
            case OidcConstants.GrantTypes.RefreshToken:
                return await ProcessRefreshTokenRequestAsync(request);
            case OidcConstants.GrantTypes.DeviceCode:
                return await ProcessDeviceCodeRequestAsync(request);
            case OidcConstants.GrantTypes.Ciba:
                return await ProcessCibaRequestAsync(request);
            default:
                return await ProcessExtensionGrantRequestAsync(request);
        }
    }

    /// <summary>
    /// Creates the response for a client credentials request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected virtual Task<TokenResponse> ProcessClientCredentialsRequestAsync(TokenRequestValidationResult request)
    {
        Logger.LogTrace("Creating response for client credentials request");

        return ProcessTokenRequestAsync(request);
    }

    /// <summary>
    /// Creates the response for a password request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected virtual Task<TokenResponse> ProcessPasswordRequestAsync(TokenRequestValidationResult request)
    {
        Logger.LogTrace("Creating response for password request");

        return ProcessTokenRequestAsync(request);
    }

    /// <summary>
    /// Creates the response for an authorization code request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">Client does not exist anymore.</exception>
    protected virtual async Task<TokenResponse> ProcessAuthorizationCodeRequestAsync(TokenRequestValidationResult request)
    {
        Logger.LogTrace("Creating response for authorization code request");

        var response = await ProcessTokenRequestAsync(request);

        if (request.ValidatedRequest.AuthorizationCode.IsOpenId)
        {
            // load the client that belongs to the authorization code
            Client client = null;
            if (request.ValidatedRequest.AuthorizationCode.ClientId != null)
            {
                // todo: do we need this check?
                client = await Clients.FindEnabledClientByIdAsync(request.ValidatedRequest.AuthorizationCode.ClientId);
            }
            if (client == null)
            {
                throw new InvalidOperationException("Client does not exist anymore.");
            }

            var tokenRequest = new TokenCreationRequest
            {
                Subject = request.ValidatedRequest.AuthorizationCode.Subject,
                ValidatedResources = request.ValidatedRequest.ValidatedResources,
                Nonce = request.ValidatedRequest.AuthorizationCode.Nonce,
                AccessTokenToHash = response.AccessToken,
                StateHash = request.ValidatedRequest.AuthorizationCode.StateHash,
                ValidatedRequest = request.ValidatedRequest
            };

            var idToken = await TokenService.CreateIdentityTokenAsync(tokenRequest);
            var jwt = await TokenService.CreateSecurityTokenAsync(idToken);
            response.IdentityToken = jwt;
        }

        return response;
    }

    /// <summary>
    /// Creates the response for a refresh token request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected virtual async Task<TokenResponse> ProcessRefreshTokenRequestAsync(TokenRequestValidationResult request)
    {
        Logger.LogTrace("Creating response for refresh token request");

        var accessToken = request.ValidatedRequest.RefreshToken.GetAccessToken(request.ValidatedRequest.RequestedResourceIndicator);

        var mustUpdate = accessToken == null || request.ValidatedRequest.Client.UpdateAccessTokenClaimsOnRefresh;
        if (mustUpdate)
        {
            var creationRequest = new TokenCreationRequest
            {
                Subject = request.ValidatedRequest.RefreshToken.Subject,
                Description = request.ValidatedRequest.RefreshToken.Description,
                ValidatedRequest = request.ValidatedRequest,
                ValidatedResources = request.ValidatedRequest.ValidatedResources
            };
            accessToken = await TokenService.CreateAccessTokenAsync(creationRequest);
        }
        else
        {
            // todo: do we want a new JTI?
            accessToken.CreationTime = Clock.UtcNow.UtcDateTime;
            accessToken.Lifetime = request.ValidatedRequest.AccessTokenLifetime;

            // always take the current request confirmation values (this would be because the proof token changed from last time)
            if (request.ValidatedRequest.Confirmation.IsPresent() && accessToken.Confirmation != request.ValidatedRequest.Confirmation)
            {
                accessToken.Confirmation = request.ValidatedRequest.Confirmation;
                mustUpdate = true; // to update the DB below
            }
        }

        var accessTokenString = await TokenService.CreateSecurityTokenAsync(accessToken);
        request.ValidatedRequest.RefreshToken.SetAccessToken(accessToken, request.ValidatedRequest.RequestedResourceIndicator);

        var handle = await RefreshTokenService.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest
        {
            Handle = request.ValidatedRequest.RefreshTokenHandle,
            RefreshToken = request.ValidatedRequest.RefreshToken,
            Client = request.ValidatedRequest.Client,
            MustUpdate = mustUpdate
        });

        return new TokenResponse
        {
            IdentityToken = await CreateIdTokenFromRefreshTokenRequestAsync(request.ValidatedRequest, accessTokenString),
            AccessToken = accessTokenString,
            AccessTokenType = request.ValidatedRequest.ProofType == ProofType.DPoP ? OidcConstants.TokenResponse.DPoPTokenType : OidcConstants.TokenResponse.BearerTokenType,
            AccessTokenLifetime = request.ValidatedRequest.AccessTokenLifetime,
            RefreshToken = handle,
            Custom = request.CustomResponse,
            Scope = request.ValidatedRequest.ValidatedResources.RawScopeValues.ToSpaceSeparatedString()
        };
    }

    /// <summary>
    /// Processes the response for device code grant request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected virtual async Task<TokenResponse> ProcessDeviceCodeRequestAsync(TokenRequestValidationResult request)
    {
        Logger.LogTrace("Creating response for device code request");

        var response = await ProcessTokenRequestAsync(request);

        if (request.ValidatedRequest.DeviceCode.IsOpenId)
        {
            // load the client that belongs to the device code
            Client client = null;
            if (request.ValidatedRequest.DeviceCode.ClientId != null)
            {
                // todo: do we need this check?
                client = await Clients.FindEnabledClientByIdAsync(request.ValidatedRequest.DeviceCode.ClientId);
            }
            if (client == null)
            {
                throw new InvalidOperationException("Client does not exist anymore.");
            }

            var tokenRequest = new TokenCreationRequest
            {
                Subject = request.ValidatedRequest.DeviceCode.Subject,
                AccessTokenToHash = response.AccessToken,
                ValidatedResources = request.ValidatedRequest.ValidatedResources,
                ValidatedRequest = request.ValidatedRequest
            };

            var idToken = await TokenService.CreateIdentityTokenAsync(tokenRequest);
            var jwt = await TokenService.CreateSecurityTokenAsync(idToken);
            response.IdentityToken = jwt;
        }

        return response;
    }

    /// <summary>
    /// Processes the response for CIBA request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected virtual async Task<TokenResponse> ProcessCibaRequestAsync(TokenRequestValidationResult request)
    {
        Logger.LogTrace("Creating response for CIBA request");

        var response = await ProcessTokenRequestAsync(request);

        // load the client that belongs to the device code
        Client client = null;
        if (request.ValidatedRequest.BackChannelAuthenticationRequest.ClientId != null)
        {
            // todo: do we need this check?
            client = await Clients.FindEnabledClientByIdAsync(request.ValidatedRequest.BackChannelAuthenticationRequest.ClientId);
        }
        if (client == null)
        {
            throw new InvalidOperationException("Client does not exist anymore.");
        }

        var tokenRequest = new TokenCreationRequest
        {
            Subject = request.ValidatedRequest.BackChannelAuthenticationRequest.Subject,
            AccessTokenToHash = response.AccessToken,
            ValidatedResources = request.ValidatedRequest.ValidatedResources,
            ValidatedRequest = request.ValidatedRequest
        };

        var idToken = await TokenService.CreateIdentityTokenAsync(tokenRequest);
        var jwt = await TokenService.CreateSecurityTokenAsync(idToken);
        response.IdentityToken = jwt;

        return response;
    }

    /// <summary>
    /// Creates the response for an extension grant request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected virtual Task<TokenResponse> ProcessExtensionGrantRequestAsync(TokenRequestValidationResult request)
    {
        Logger.LogTrace("Creating response for extension grant request");

        return ProcessTokenRequestAsync(request);
    }

    /// <summary>
    /// Creates a response for a token request containing an access token and a
    /// refresh token if requested.
    /// </summary>
    protected virtual async Task<TokenResponse> ProcessTokenRequestAsync(TokenRequestValidationResult validationResult)
    {
        var (accessToken, refreshToken) = await CreateAccessTokenAsync(validationResult.ValidatedRequest);
        var response = new TokenResponse
        {
            AccessToken = accessToken,
            AccessTokenType = validationResult.ValidatedRequest.ProofType == ProofType.DPoP ? OidcConstants.TokenResponse.DPoPTokenType : OidcConstants.TokenResponse.BearerTokenType,
            AccessTokenLifetime = validationResult.ValidatedRequest.AccessTokenLifetime,
            Custom = validationResult.CustomResponse,
            Scope = validationResult.ValidatedRequest.ValidatedResources.RawScopeValues.ToSpaceSeparatedString()
        };

        if (refreshToken.IsPresent())
        {
            response.RefreshToken = refreshToken;
        }

        return response;
    }


    /// <summary>
    /// Creates the access/refresh token.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">Client does not exist anymore.</exception>
    protected virtual async Task<(string accessToken, string refreshToken)> CreateAccessTokenAsync(ValidatedTokenRequest request)
    {
        var tokenRequest = new TokenCreationRequest
        {
            Subject = request.Subject,
            ValidatedResources = request.ValidatedResources,
            ValidatedRequest = request
        };

        var createRefreshToken = request.ValidatedResources.Resources.OfflineAccess;
        var authorizedScopes = Enumerable.Empty<string>();
        IEnumerable<string> authorizedResourceIndicators = null;

        if (request.AuthorizationCode != null)
        {
            // load the client that belongs to the authorization code
            Client client = null;
            if (request.AuthorizationCode.ClientId != null)
            {
                // todo: do we need this check?
                client = await Clients.FindEnabledClientByIdAsync(request.AuthorizationCode.ClientId);
            }
            if (client == null)
            {
                throw new InvalidOperationException("Client does not exist anymore.");
            }

            tokenRequest.Subject = request.AuthorizationCode.Subject;
            tokenRequest.Description = request.AuthorizationCode.Description;

            authorizedScopes = request.AuthorizationCode.RequestedScopes;
            authorizedResourceIndicators = request.AuthorizationCode.RequestedResourceIndicators;
        }
        else if (request.BackChannelAuthenticationRequest != null)
        {
            // load the client that belongs to the authorization code
            Client client = null;
            if (request.BackChannelAuthenticationRequest.ClientId != null)
            {
                // todo: do we need this check?
                client = await Clients.FindEnabledClientByIdAsync(request.BackChannelAuthenticationRequest.ClientId);
            }
            if (client == null)
            {
                throw new InvalidOperationException("Client does not exist anymore.");
            }

            tokenRequest.Subject = request.BackChannelAuthenticationRequest.Subject;
            tokenRequest.Description = request.BackChannelAuthenticationRequest.Description;

            authorizedScopes = request.BackChannelAuthenticationRequest.AuthorizedScopes;
            // TODO: should this come from the current request instead of the ciba request
            authorizedResourceIndicators = request.BackChannelAuthenticationRequest.RequestedResourceIndicators;
        }
        else if (request.DeviceCode != null)
        {
            Client client = null;
            if (request.DeviceCode.ClientId != null)
            {
                // todo: do we need this check?
                client = await Clients.FindEnabledClientByIdAsync(request.DeviceCode.ClientId);
            }
            if (client == null)
            {
                throw new InvalidOperationException("Client does not exist anymore.");
            }

            tokenRequest.Subject = request.DeviceCode.Subject;
            tokenRequest.Description = request.DeviceCode.Description;

            authorizedScopes = request.DeviceCode.AuthorizedScopes;
        }
        else
        {
            authorizedScopes = request.ValidatedResources.RawScopeValues;
        }

        var at = await TokenService.CreateAccessTokenAsync(tokenRequest);
        var accessToken = await TokenService.CreateSecurityTokenAsync(at);

        if (createRefreshToken)
        {
            var rtRequest = new RefreshTokenCreationRequest
            {
                Client = request.Client,
                Subject = tokenRequest.Subject,
                Description = tokenRequest.Description,
                AuthorizedScopes = authorizedScopes,
                AuthorizedResourceIndicators = authorizedResourceIndicators,
                AccessToken = at,
                RequestedResourceIndicator = request.RequestedResourceIndicator,
                ProofType = request.ProofType
            };
            var refreshToken = await RefreshTokenService.CreateRefreshTokenAsync(rtRequest);
            return (accessToken, refreshToken);
        }

        return (accessToken, null);
    }

    /// <summary>
    /// Creates an id_token for a refresh token request if identity resources have been requested.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="newAccessToken">The new access token.</param>
    /// <returns></returns>
    protected virtual async Task<string> CreateIdTokenFromRefreshTokenRequestAsync(ValidatedTokenRequest request, string newAccessToken)
    {
        if (request.RefreshToken.AuthorizedScopes.Contains(OidcConstants.StandardScopes.OpenId))
        {
            var tokenRequest = new TokenCreationRequest
            {
                Subject = request.RefreshToken.Subject,
                ValidatedResources = request.ValidatedResources,
                ValidatedRequest = request,
                AccessTokenToHash = newAccessToken
            };

            var idToken = await TokenService.CreateIdentityTokenAsync(tokenRequest);
            return await TokenService.CreateSecurityTokenAsync(idToken);
        }

        return null;
    }
}
