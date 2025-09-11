// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using static Duende.IdentityModel.OidcConstants;

namespace Duende.IdentityServer.Hosting.LocalApiAuthentication;

/// <summary>
/// Authentication handler for validating access token from the local IdentityServer
/// </summary>
public class LocalApiAuthenticationHandler : AuthenticationHandler<LocalApiAuthenticationOptions>
{
    private readonly ITokenValidator _tokenValidator;
    private readonly IDPoPProofValidator _dpopValidator;
    private readonly IClientStore _clientStore;
    private readonly ILogger _logger;

    /// <inheritdoc />
    public LocalApiAuthenticationHandler(
        IOptionsMonitor<LocalApiAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenValidator tokenValidator,
        IDPoPProofValidator dpopValidator,
        IClientStore clientStore)
        : base(options, logger, encoder)
    {
        _tokenValidator = tokenValidator;
        _dpopValidator = dpopValidator;
        _clientStore = clientStore;
        _logger = logger.CreateLogger<LocalApiAuthenticationHandler>();
    }

    /// <summary>
    /// The handler calls methods on the events which give the application control at certain points where processing is occurring. 
    /// If it is not provided a default instance is supplied which does nothing when the methods are called.
    /// </summary>
    protected new LocalApiAuthenticationEvents Events
    {
        get => (LocalApiAuthenticationEvents)base.Events;
        set => base.Events = value;
    }

    /// <inheritdoc/>
    protected override Task<object> CreateEventsAsync() => Task.FromResult<object>(new LocalApiAuthenticationEvents());

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        _logger.LogTrace("HandleAuthenticateAsync called");

        string token = null;

        string authorization = Request.Headers.Authorization;

        if (string.IsNullOrEmpty(authorization))
        {
            return AuthenticateResult.NoResult();
        }

        var wasDPoPToken = false;

        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            if (Options.TokenMode == LocalApiTokenMode.DPoPOnly)
            {
                _logger.LogTrace("Bearer token sent, but mode is DPoP only. Ignoring token.");
                return AuthenticateResult.NoResult();
            }

            token = authorization["Bearer ".Length..].Trim();
        }
        else if (authorization.StartsWith("DPoP ", StringComparison.OrdinalIgnoreCase))
        {
            if (Options.TokenMode == LocalApiTokenMode.BearerOnly)
            {
                _logger.LogTrace("DPoP token sent, but mode is Bearer only. Ignoring token.");
                return AuthenticateResult.NoResult();
            }

            wasDPoPToken = true;
            token = authorization["DPoP ".Length..].Trim();
        }

        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("No Access Token is sent.");
        }

        _logger.LogTrace("Token found: {token}", token);

        var tokenResult = await _tokenValidator.ValidateAccessTokenAsync(token, Options.ExpectedScope);
        if (tokenResult.IsError)
        {
            _logger.LogTrace("Failed to validate the token");

            return AuthenticateResult.Fail(tokenResult.Error);
        }

        if (wasDPoPToken)
        {
            var clientId = tokenResult.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.ClientId)?.Value;
            var client = await _clientStore.FindEnabledClientByIdAsync(clientId);
            if (client == null)
            {
                // invalid or missing client id
                return AuthenticateResult.Fail("Invalid or missing client_id from access token");
            }

            var proofToken = Context.Request.Headers[OidcConstants.HttpHeaders.DPoP].FirstOrDefault();
            var validationContext = new DPoPProofValidatonContext
            {
                ProofToken = proofToken,
                Method = Context.Request.Method,
                Url = Context.Request.Scheme + "://" + Context.Request.Host + Context.Request.PathBase + Context.Request.Path,
                ValidateAccessToken = true,
                AccessToken = token,
                AccessTokenClaims = tokenResult.Claims,
                ExpirationValidationMode = client.DPoPValidationMode,
                ClientClockSkew = client.DPoPClockSkew,
            };

            var dpopResult = await _dpopValidator.ValidateAsync(validationContext);
            if (dpopResult.IsError)
            {
                // we need to stash these values away so they are available later when the Challenge method is called later
                Context.Items["DPoP-Error"] = dpopResult.Error;
                if (!string.IsNullOrWhiteSpace(dpopResult.ErrorDescription))
                {
                    Context.Items["DPoP-ErrorDescription"] = dpopResult.ErrorDescription;
                }
                if (!string.IsNullOrWhiteSpace(dpopResult.ServerIssuedNonce))
                {
                    Context.Items["DPoP-Nonce"] = dpopResult.ServerIssuedNonce;
                }

                // fails the result
                return AuthenticateResult.Fail(dpopResult.ErrorDescription ?? dpopResult.Error);
            }
        }
        else if (Options.TokenMode == LocalApiTokenMode.DPoPAndBearer)
        {
            // if the scheme used was not DPoP, then it was Bearer
            // and if a access token was presented with a cnf, then the 
            // client should have sent it as DPoP, so we fail the request
            if (tokenResult.Claims.Any(x => x.Type == JwtClaimTypes.Confirmation))
            {
                Context.Items["Bearer-Error"] = "invalid_token";
                Context.Items["Bearer-ErrorDescription"] = "Must use DPoP when using an access token with a 'cnf' claim";
                return AuthenticateResult.Fail("Must use DPoP when using an access token with a 'cnf' claim");
            }
        }

        _logger.LogTrace("Successfully validated the token.");

        var claimsIdentity = new ClaimsIdentity(tokenResult.Claims, Scheme.Name, JwtClaimTypes.Name, JwtClaimTypes.Role);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        var authenticationProperties = new AuthenticationProperties();

        if (Options.SaveToken)
        {
            authenticationProperties.StoreTokens(new[]
            {
                new AuthenticationToken { Name = "access_token", Value = token }
            });
        }

        var claimsTransformationContext = new ClaimsTransformationContext
        {
            Principal = claimsPrincipal,
            HttpContext = Context
        };

        await Events.ClaimsTransformation(claimsTransformationContext);

        var authenticationTicket = new AuthenticationTicket(claimsTransformationContext.Principal, authenticationProperties, Scheme.Name);
        return AuthenticateResult.Success(authenticationTicket);
    }

    /// <inheritdoc/>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;

        // Format:
        // WWW-Authenticate: DPoP error="invalid_dpop_proof", error_description="Invalid 'iat' value."

        var sb = new StringBuilder();

        if (Options.TokenMode == LocalApiTokenMode.BearerOnly || Options.TokenMode == LocalApiTokenMode.DPoPAndBearer)
        {
            _ = sb.Append("Bearer");

            if (Context.Items.ContainsKey("Bearer-Error"))
            {
                _ = sb.Append(" error=\"");
                _ = sb.Append(Context.Items["Bearer-Error"] as string);
                _ = sb.Append('\"');

                if (Context.Items.ContainsKey("Bearer-ErrorDescription"))
                {
                    _ = sb.Append(", error_description=\"");
                    _ = sb.Append(Context.Items["Bearer-ErrorDescription"] as string);
                    _ = sb.Append('\"');
                }
            }
        }

        if (Options.TokenMode == LocalApiTokenMode.DPoPOnly || Options.TokenMode == LocalApiTokenMode.DPoPAndBearer)
        {
            if (sb.Length > 0)
            {
                _ = sb.Append(", ");
            }

            _ = sb.Append("DPoP");

            if (Context.Items.ContainsKey("DPoP-Error"))
            {
                _ = sb.Append(" error=\"");
                _ = sb.Append(Context.Items["DPoP-Error"] as string);
                _ = sb.Append('\"');

                if (Context.Items.ContainsKey("DPoP-ErrorDescription"))
                {
                    _ = sb.Append(", error_description=\"");
                    _ = sb.Append(Context.Items["DPoP-ErrorDescription"] as string);
                    _ = sb.Append('\"');
                }
            }

            // Emit a nonce if we have it
            if (Context.Items.ContainsKey("DPoP-Nonce"))
            {
                // this is from our validator above
                var nonce = Context.Items["DPoP-Nonce"] as string;
                Context.Response.Headers[HttpHeaders.DPoPNonce] = nonce;
            }
            else if (properties.Items.TryGetValue("DPoP-Nonce", out var nonce))
            {
                // this allows the API itself to set the nonce
                Context.Response.Headers[HttpHeaders.DPoPNonce] = nonce;
            }
        }

        if (sb.Length > 0)
        {
            if (Response.Headers.ContainsKey(HeaderNames.WWWAuthenticate))
            {

                throw new InvalidOperationException("Attempted to set the WWW-Authenticate header when it is already set");
            }
            Response.Headers[HeaderNames.WWWAuthenticate] = sb.ToString();
        }

        return Task.CompletedTask;
    }
}
