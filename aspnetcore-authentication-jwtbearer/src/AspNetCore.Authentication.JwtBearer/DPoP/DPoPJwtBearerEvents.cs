// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using static Duende.IdentityModel.OidcConstants;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Events for the Jwt Bearer authentication handler that enable DPoP.
/// </summary>
public class DPoPJwtBearerEvents : JwtBearerEvents
{
    private readonly IOptionsMonitor<DPoPOptions> _optionsMonitor;
    private readonly IDPoPProofValidator _validator;
    private readonly ILogger<DPoPJwtBearerEvents> _logger;

    /// <summary>
    /// Constructs a new instance of <see cref="DPoPJwtBearerEvents"/>. 
    /// </summary>
    /// <param name="optionsMonitor"></param>
    /// <param name="validator"></param>
    /// <param name="logger"></param>
    public DPoPJwtBearerEvents(IOptionsMonitor<DPoPOptions> optionsMonitor, IDPoPProofValidator validator, ILogger<DPoPJwtBearerEvents> logger)
    {
        _optionsMonitor = optionsMonitor;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to retrieve a DPoP access token from incoming requests, and
    /// optionally enforces its presence.
    /// </summary>
    public override Task MessageReceived(MessageReceivedContext context)
    {
        _logger.LogDebug("MessageReceived invoked in a JWT bearer authentication scheme using DPoP");

        var dpopOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (TryGetDPoPAccessToken(context.HttpContext.Request, dpopOptions.ProofTokenMaxLength, out var token))
        {
            context.Token = token;
        }
        else if (dpopOptions.TokenMode == DPoPMode.DPoPOnly)
        {
            // this rejects the attempt for this handler,
            // since we don't want to attempt Bearer given the Mode
            context.NoResult();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures that a valid DPoP proof token accompanies DPoP access tokens.
    /// </summary>
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        _logger.LogDebug("TokenValidated invoked in a JWT bearer authentication scheme using DPoP");

        var dPoPOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (TryGetDPoPAccessToken(context.HttpContext.Request, dPoPOptions.ProofTokenMaxLength, out var at))
        {
            var proofToken = context.HttpContext.Request.GetDPoPProofToken();
            if (proofToken == null)
            {
                throw new InvalidOperationException("Missing DPoP (proof token) HTTP header");
            }

            var result = await _validator.Validate(new DPoPProofValidationContext
            {
                Scheme = context.Scheme.Name,
                ProofToken = proofToken,
                AccessToken = at,
                AccessTokenClaims = context.Principal?.Claims ?? [],
                Method = context.HttpContext.Request.Method,
                Url = context.HttpContext.Request.Scheme + "://" + context.HttpContext.Request.Host + context.HttpContext.Request.PathBase + context.HttpContext.Request.Path
            });

            if (result.IsError)
            {
                context.Fail(result.ErrorDescription ?? result.Error ?? throw new Exception("No ErrorDescription or Error set."));

                // we need to stash these values away, so they are available later when the Challenge method is called later
                context.HttpContext.Items["DPoP-Error"] = result.Error;
                if (!string.IsNullOrWhiteSpace(result.ErrorDescription))
                {
                    context.HttpContext.Items["DPoP-ErrorDescription"] = result.ErrorDescription;
                }
                if (!string.IsNullOrWhiteSpace(result.ServerIssuedNonce))
                {
                    context.HttpContext.Items["DPoP-Nonce"] = result.ServerIssuedNonce;
                }
            }
        }
        else if (dPoPOptions.TokenMode == DPoPMode.DPoPAndBearer)
        {
            // if the scheme used was not DPoP, then it was Bearer
            // and if an access token was presented with a cnf, then the 
            // client should have sent it as DPoP, so we fail the request
            if (context.Principal?.HasClaim(x => x.Type == JwtClaimTypes.Confirmation) ?? false)
            {
                context.HttpContext.Items["Bearer-ErrorDescription"] = "Must use DPoP when using an access token with a 'cnf' claim";
                context.Fail("Must use DPoP when using an access token with a 'cnf' claim");
            }
        }
    }

    private const string DPoPPrefix = AuthenticationSchemes.AuthorizationHeaderDPoP + " ";

    /// <summary>
    /// Checks if the HTTP authorization header's 'scheme' is DPoP.
    /// </summary>
    protected static bool IsDPoPAuthorizationScheme(HttpRequest request)
    {
        var authz = request.Headers.Authorization.FirstOrDefault();
        return authz?.StartsWith(DPoPPrefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Attempts to retrieve a DPoP access token from an <see cref="HttpRequest"/>.
    /// </summary>
    public bool TryGetDPoPAccessToken(HttpRequest request,
        int maxLength,
        [NotNullWhen(true)] out string? token)
    {
        token = null;

        var authz = request.Headers.Authorization.FirstOrDefault();
        if (authz != null && authz.Length >= maxLength)
        {
            _logger.LogInformation("DPoP proof rejected because it exceeded ProofTokenMaxLength.");
            return false;
        }
        if (authz?.StartsWith(DPoPPrefix, StringComparison.OrdinalIgnoreCase) == true)
        {
            token = authz[DPoPPrefix.Length..].Trim();
            _logger.LogDebug("DPoP access token found");
            return true;
        }
        _logger.LogDebug("DPoP access token not found");
        return false;
    }

    /// <summary>
    /// Adds the necessary HTTP headers and response codes for DPoP error
    /// handling and nonce generation.
    /// </summary>
    public override Task Challenge(JwtBearerChallengeContext context)
    {
        _logger.LogDebug("Challenge invoked in a JWT bearer authentication scheme using DPoP");
        var dPoPOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (dPoPOptions.TokenMode == DPoPMode.DPoPOnly)
        {
            // if we are using DPoP only, then we don't need/want the default
            // JwtBearerHandler to add its WWW-Authenticate response header,
            // so we have to set the status code ourselves
            context.Response.StatusCode = 401;
            context.HandleResponse();
        }
        else if (context.HttpContext.Items.TryGetValue("Bearer-ErrorDescription", out var description))
        {
            context.ErrorDescription = (string?) description;
        }

        if (IsDPoPAuthorizationScheme(context.HttpContext.Request))
        {
            // if we are challenging due to dpop, then don't allow bearer www-auth to emit an error
            context.Error = null;
        }

        // now we always want to add our WWW-Authenticate for DPoP
        // For example:
        // WWW-Authenticate: DPoP error="invalid_dpop_proof", error_description="Invalid 'iat' value."
        var sb = new StringBuilder();
        sb.Append(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP);

        if (context.HttpContext.Items.TryGetValue("DPoP-Error", out var error))
        {
            sb.Append(" error=\"");
            sb.Append(error);
            sb.Append('\"');

            if (context.HttpContext.Items.TryGetValue("DPoP-ErrorDescription", out var description))
            {
                sb.Append(", error_description=\"");
                sb.Append(description);
                sb.Append('\"');
            }
        }

        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, sb.ToString());

        if (context.HttpContext.Items.TryGetValue("DPoP-Nonce", out var nonceValue))
        {
            context.Response.Headers[HttpHeaders.DPoPNonce] = nonceValue as string;
        }
        else
        {
            var nonce = context.Properties.GetDPoPNonce();
            if (nonce != null)
            {
                context.Response.Headers[HttpHeaders.DPoPNonce] = nonce;
            }
        }

        return Task.CompletedTask;
    }
}
