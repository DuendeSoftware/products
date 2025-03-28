// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using static Duende.IdentityServer.Constants;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// The introspection request validator
/// </summary>
/// <seealso cref="IIntrospectionRequestValidator" />
internal class IntrospectionRequestValidator : IIntrospectionRequestValidator
{
    private readonly ILogger _logger;
    private readonly ITokenValidator _tokenValidator;
    private readonly IRefreshTokenService _refreshTokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntrospectionRequestValidator"/> class.
    /// </summary>
    /// <param name="tokenValidator">The token validator.</param>
    /// <param name="refreshTokenService"></param>
    /// <param name="logger">The logger.</param>
    public IntrospectionRequestValidator(ITokenValidator tokenValidator, IRefreshTokenService refreshTokenService, ILogger<IntrospectionRequestValidator> logger)
    {
        _tokenValidator = tokenValidator;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IntrospectionRequestValidationResult> ValidateAsync(IntrospectionRequestValidationContext context)
    {
        var parameters = context.Parameters;
        var api = context.Api;
        var client = context.Client;

        if (api != null && client != null)
        {
            throw new ArgumentException("Can't validate with both an ApiResource and a Client.");
        }
        if (api == null && client == null)
        {
            throw new ArgumentException("Can't validate without an ApiResource or a Client.");
        }

        using var activity = Tracing.BasicActivitySource.StartActivity("IntrospectionRequestValidator.Validate");

        _logger.LogDebug("Introspection request validation started.");

        // retrieve required token
        var token = parameters.Get("token");
        if (token == null)
        {
            _logger.LogError("Token is missing");

            return new IntrospectionRequestValidationResult
            {
                IsError = true,
                Api = api,
                Client = client,
                Error = "missing_token",
                Parameters = parameters
            };
        }

        var hint = parameters.Get("token_type_hint");
        if (hint.IsPresent())
        {
            if (Constants.SupportedTokenTypeHints.Contains(hint))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var sanitized = hint.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                    _logger.LogDebug("Token type hint found in request: {tokenTypeHint}", sanitized);
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var sanitized = hint.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                    _logger.LogDebug("Unsupported token type hint found in request: {tokenTypeHint}", sanitized);
                }
                hint = null; // Discard an unknown hint, in line with RFC 7662
            }
        }

        // the result we care about
        IEnumerable<Claim> claims;

        if (api != null)
        {
            // APIs can only introspect access tokens. We ignore the hint and just immediately try to 
            // validate the token as an access token. If that fails, claims will be null and 
            // we'll return { "isActive": false }.
            claims = await GetAccessTokenClaimsAsync(token);
        }
        else
        {
            // Clients can introspect access tokens and refresh tokens. They can pass a hint to us to
            // help us introspect, but RFC 7662 says if the hint is wrong we have to fall back to
            // trying the other type.
            //
            // RFC 7662 (OAuth 2.0 Token Introspection), Section 2.1:
            //
            // > If the server is unable to locate the token using the given hint,
            // > it MUST extend its search across all of its supported token types.
            // > An authorization server MAY ignore this parameter, particularly if
            // > it is able to detect the token type automatically.

            if (hint.IsMissing() || hint == TokenTypeHints.AccessToken)
            {
                // try access token
                claims = await GetAccessTokenClaimsAsync(token, client);
                if (claims == null)
                {
                    // fall back to refresh token
                    if (hint.IsPresent())
                    {
                        _logger.LogDebug("Failed to validate token as access token. Possible incorrect token_type_hint parameter.");
                    }
                    claims = await GetRefreshTokenClaimsAsync(token, client);
                }
            }
            else
            {
                // try refresh token
                claims = await GetRefreshTokenClaimsAsync(token, client);
                if (claims == null)
                {
                    // fall back to access token
                    if (hint.IsPresent())
                    {
                        _logger.LogDebug("Failed to validate token as refresh token. Possible incorrect token_type_hint parameter.");
                    }
                    claims = await GetAccessTokenClaimsAsync(token, client);
                }
            }
        }


        if (claims != null)
        {
            _logger.LogDebug("Introspection request validation successful.");

            return new IntrospectionRequestValidationResult
            {
                IsActive = true,
                IsError = false,
                Token = token,
                Api = api,
                Client = client,
                Claims = claims,
                Parameters = parameters
            };
        }

        // if we get here then fail
        _logger.LogDebug("Token is invalid.");

        return new IntrospectionRequestValidationResult
        {
            IsActive = false,
            IsError = false,
            Token = token,
            Api = api,
            Client = client,
            Parameters = parameters
        };
    }

    /// <summary>
    /// Attempt to obtain the claims for a token as a refresh token for a client.
    /// </summary>
    private async Task<IEnumerable<Claim>> GetRefreshTokenClaimsAsync(string token, Client client)
    {
        var refreshValidationResult = await _refreshTokenService.ValidateRefreshTokenAsync(token, client);
        if (!refreshValidationResult.IsError)
        {
            var iat = refreshValidationResult.RefreshToken.CreationTime.ToEpochTime();
            var claims = new List<Claim>
            {
                new Claim("client_id", client.ClientId),
                new Claim("token_type", TokenTypeHints.RefreshToken),
                new Claim("iat", iat.ToString(), ClaimValueTypes.Integer),
                new Claim("exp", (iat + refreshValidationResult.RefreshToken.Lifetime).ToString(), ClaimValueTypes.Integer),
                new Claim("sub", refreshValidationResult.RefreshToken.SubjectId),
            };

            foreach (var scope in refreshValidationResult.RefreshToken.AuthorizedScopes)
            {
                claims.Add(new Claim("scope", scope));
            }

            return claims;
        }

        return null;
    }

    /// <summary>
    /// Attempt to obtain the claims for a token as an access token, and validate that it belongs to the client. 
    /// </summary>
    private async Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(string token, Client client)
    {
        var tokenValidationResult = await _tokenValidator.ValidateAccessTokenAsync(token);
        if (!tokenValidationResult.IsError)
        {
            var claims = tokenValidationResult.Claims.ToList();

            var tokenClientId = claims.SingleOrDefault(x => x.Type == JwtClaimTypes.ClientId)?.Value;
            if (tokenClientId == client.ClientId)
            {
                _logger.LogDebug("Validated access token");
                claims.Add(new Claim("token_type", TokenTypeHints.AccessToken));
                return claims;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempt to obtain the claims for a token as an access token. This overload does no validation that the
    /// token belongs to a particular client, and is intended for use when we have an API caller (any API can 
    /// introspect a token). 
    /// </summary>
    private async Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(string token)
    {
        var tokenValidationResult = await _tokenValidator.ValidateAccessTokenAsync(token);
        if (!tokenValidationResult.IsError)
        {
            _logger.LogDebug("Validated access token");
            return tokenValidationResult.Claims;
        }

        return null;
    }
}
