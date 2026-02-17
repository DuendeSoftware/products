// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default token service
/// </summary>
public class DefaultTokenService : ITokenService
{
    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The claims provider
    /// </summary>
    protected readonly IClaimsService ClaimsProvider;

    /// <summary>
    /// The reference token store
    /// </summary>
    protected readonly IReferenceTokenStore ReferenceTokenStore;

    /// <summary>
    /// The signing service
    /// </summary>
    protected readonly ITokenCreationService CreationService;

    /// <summary>
    /// The clock
    /// </summary>
    protected readonly TimeProvider Clock;

    /// <summary>
    /// The key material service
    /// </summary>
    protected readonly IKeyMaterialService KeyMaterialService;

    /// <summary>
    /// The IdentityServer options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTokenService" /> class.
    /// </summary>
    /// <param name="claimsProvider">The claims provider.</param>
    /// <param name="referenceTokenStore">The reference token store.</param>
    /// <param name="creationService">The signing service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="keyMaterialService"></param>
    /// <param name="options">The IdentityServer options</param>
    /// <param name="logger">The logger.</param>
    public DefaultTokenService(
        IClaimsService claimsProvider,
        IReferenceTokenStore referenceTokenStore,
        ITokenCreationService creationService,
        TimeProvider clock,
        IKeyMaterialService keyMaterialService,
        IdentityServerOptions options,
        ILogger<DefaultTokenService> logger)
    {
        ClaimsProvider = claimsProvider;
        ReferenceTokenStore = referenceTokenStore;
        CreationService = creationService;
        Clock = clock;
        KeyMaterialService = keyMaterialService;
        Options = options;
        Logger = logger;
    }

    /// <summary>
    /// Creates an identity token.
    /// </summary>
    /// <param name="request">The token creation request.</param>
    /// <returns>
    /// An identity token
    /// </returns>
    public virtual async Task<Token> CreateIdentityTokenAsync(TokenCreationRequest request)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultTokenService.CreateIdentityToken");

        Logger.LogTrace("Creating identity token");
        request.Validate();

        // todo: Dom, add a test for this. validate the at and c hashes are correct for the id_token when the client's alg doesn't match the server default.
        var credential = await KeyMaterialService.GetSigningCredentialsAsync(request.ValidatedRequest.Client.AllowedIdentityTokenSigningAlgorithms);
        if (credential == null)
        {
            throw new InvalidOperationException("No signing credential is configured.");
        }

        var signingAlgorithm = credential.Algorithm;

        // host provided claims
        var claims = new List<Claim>();

        // if nonce was sent, must be mirrored in id token
        if (request.Nonce.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.Nonce, request.Nonce));
        }

        // add at_hash claim
        if (request.AccessTokenToHash.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.AccessTokenHash, CryptoHelper.CreateHashClaimValue(request.AccessTokenToHash, signingAlgorithm)));
        }

        // add c_hash claim
        if (request.AuthorizationCodeToHash.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.AuthorizationCodeHash, CryptoHelper.CreateHashClaimValue(request.AuthorizationCodeToHash, signingAlgorithm)));
        }

        // add s_hash claim
        if (request.StateHash.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.StateHash, request.StateHash));
        }

        // add sid if present
        if (request.ValidatedRequest.SessionId.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.SessionId, request.ValidatedRequest.SessionId));
        }

        claims.AddRange(await ClaimsProvider.GetIdentityTokenClaimsAsync(
            request.Subject,
            request.ValidatedResources,
            request.IncludeAllIdentityClaims,
            request.ValidatedRequest));

        var issuer = request.ValidatedRequest.IssuerName;
        var token = new Token(OidcConstants.TokenTypes.IdentityToken)
        {
            CreationTime = Clock.GetUtcNow().UtcDateTime,
            Audiences = { request.ValidatedRequest.Client.ClientId },
            Issuer = issuer,
            Lifetime = request.ValidatedRequest.Client.IdentityTokenLifetime,
            Claims = claims.Distinct(new ClaimComparer()).ToList(),
            ClientId = request.ValidatedRequest.Client.ClientId,
            AccessTokenType = request.ValidatedRequest.AccessTokenType,
            AllowedSigningAlgorithms = request.ValidatedRequest.Client.AllowedIdentityTokenSigningAlgorithms
        };

        return token;
    }

    /// <summary>
    /// Creates an access token.
    /// </summary>
    /// <param name="request">The token creation request.</param>
    /// <returns>
    /// An access token
    /// </returns>
    public virtual async Task<Token> CreateAccessTokenAsync(TokenCreationRequest request)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultTokenService.CreateAccessToken");

        Logger.LogTrace("Creating access token");
        request.Validate();

        var claims = new List<Claim>();
        claims.AddRange(await ClaimsProvider.GetAccessTokenClaimsAsync(
            request.Subject,
            request.ValidatedResources,
            request.ValidatedRequest));

        if (request.ValidatedRequest.SessionId.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.SessionId, request.ValidatedRequest.SessionId));
        }

        var issuer = request.ValidatedRequest.IssuerName;
        var token = new Token(OidcConstants.TokenTypes.AccessToken)
        {
            CreationTime = Clock.GetUtcNow().UtcDateTime,
            Issuer = issuer,
            Lifetime = request.ValidatedRequest.AccessTokenLifetime,
            IncludeJwtId = request.ValidatedRequest.Client.IncludeJwtId,
            Claims = claims.Distinct(new ClaimComparer()).ToList(),
            ClientId = request.ValidatedRequest.Client.ClientId,
            Description = request.Description,
            AccessTokenType = request.ValidatedRequest.AccessTokenType,
            AllowedSigningAlgorithms = request.ValidatedResources.Resources.ApiResources.FindMatchingSigningAlgorithms()
        };

        // add aud based on ApiResources in the validated request
        foreach (var aud in request.ValidatedResources.Resources.ApiResources.Select(x => x.Name).Distinct())
        {
            token.Audiences.Add(aud);
        }

        if (Options.EmitStaticAudienceClaim)
        {
#pragma warning disable CA1863 // Would require changing a public const on a public class and be a breaking change
            token.Audiences.Add(string.Format(CultureInfo.InvariantCulture, IdentityServerConstants.AccessTokenAudience, issuer.EnsureTrailingSlash()));
#pragma warning restore CA1863
        }

        // add cnf if present
        if (request.ValidatedRequest.Confirmation.IsPresent())
        {
            token.Confirmation = request.ValidatedRequest.Confirmation;
        }

        return token;
    }

    /// <summary>
    /// Creates a serialized and protected security token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>
    /// A security token in serialized form
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Invalid token type.</exception>
    public virtual async Task<string> CreateSecurityTokenAsync(Token token)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultTokenService.CreateSecurityToken");

        string tokenResult;

        if (token.Type == OidcConstants.TokenTypes.AccessToken)
        {
            var currentJwtId = token.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.JwtId);
            if (token.IncludeJwtId || (currentJwtId != null && token.Version < 5))
            {
                if (currentJwtId != null)
                {
                    token.Claims.Remove(currentJwtId);
                }
                token.Claims.Add(new Claim(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)));
            }

            if (token.AccessTokenType == AccessTokenType.Jwt)
            {
                Logger.LogTrace("Creating JWT access token");

                tokenResult = await CreationService.CreateTokenAsync(token);
            }
            else
            {
                Logger.LogTrace("Creating reference access token");

                var handle = await ReferenceTokenStore.StoreReferenceTokenAsync(token);

                tokenResult = handle;
            }
        }
        else if (token.Type == OidcConstants.TokenTypes.IdentityToken)
        {
            Logger.LogTrace("Creating JWT identity token");

            tokenResult = await CreationService.CreateTokenAsync(token);
        }
        else
        {
            throw new InvalidOperationException("Invalid token type.");
        }

        return tokenResult;
    }
}
