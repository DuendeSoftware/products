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
internal class DPoPJwtBearerEvents
{
    private readonly IOptionsMonitor<DPoPOptions> _optionsMonitor;
    private readonly IDPoPProofValidator _validator;
    private readonly ILogger<DPoPJwtBearerEvents> _logger;

    /// <summary>
    /// Constructs a new instance of <see cref="DPoPJwtBearerEvents"/>.
    /// </summary>
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
    public Task MessageReceived(MessageReceivedContext context)
    {
        using var activity = Tracing.ActivitySource.StartActivity("DPoPJwtBearerEvents.MessageReceived");

        var dpopOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (TryGetDPoPAccessToken(context.HttpContext.Request, dpopOptions.ProofTokenMaxLength, out var token))
        {
            context.Token = token;
        }
        else if (!dpopOptions.AllowBearerTokens)
        {
            // this rejects the attempt for this handler.
            // we don't want to attempt Bearer when it is disabled by configuration.
            context.NoResult();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures that a valid DPoP proof token accompanies DPoP access tokens.
    /// </summary>
    public async Task TokenValidated(TokenValidatedContext context)
    {
        using var activity = Tracing.ActivitySource.StartActivity("DPoPJwtBearerEvents.TokenValidated");

        var dPoPOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (TryGetDPoPAccessToken(context.HttpContext.Request, dPoPOptions.ProofTokenMaxLength, out var at))
        {
            string proofToken;
            if (context.Request.Headers.TryGetValue(HttpHeaders.DPoP, out var dPopHeader))
            {
                if (dPopHeader.Count > 1)
                {
                    context.HttpContext.Items["DPoP-Error"] = "invalid_request";
                    context.Fail("Multiple DPoP headers found");
                    return;
                }

                proofToken = dPopHeader.First()!;
            }
            else
            {
                context.HttpContext.Items["DPoP-Error"] = "invalid_request";
                context.Fail("No DPoP header found");
                return;
            }

            var result = await _validator.Validate(new DPoPProofValidationContext
            {
                Options = dPoPOptions,
                Scheme = context.Scheme.Name,
                ProofToken = proofToken,
                AccessToken = at,
                AccessTokenClaims = context.Principal?.Claims ?? [],
                ExpectedMethod = context.HttpContext.Request.Method,
                ExpectedUrl = context.HttpContext.Request.Scheme + "://" + context.HttpContext.Request.Host + context.HttpContext.Request.PathBase + context.HttpContext.Request.Path
            });

            if (result.IsError)
            {
                context.Fail(result.ErrorDescription ?? result.Error ?? throw new Exception("No ErrorDescription or Error set."));

                // we need to stash these values away, so they are available later when the Challenge method is called
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    context.HttpContext.Items["DPoP-Error"] = result.Error;
                }
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
        else if (dPoPOptions.AllowBearerTokens)
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
    private static bool IsDPoPAuthorizationScheme(HttpRequest request)
    {
        var authz = request.Headers.Authorization.FirstOrDefault();
        return authz?.StartsWith(DPoPPrefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Attempts to retrieve a DPoP access token from an <see cref="HttpRequest"/>.
    /// </summary>
    private bool TryGetDPoPAccessToken(HttpRequest request,
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
    public Task Challenge(JwtBearerChallengeContext context)
    {
        using var activity = Tracing.ActivitySource.StartActivity("DPoPJwtBearerEvents.Challenge");

        var dPoPOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (!dPoPOptions.AllowBearerTokens)
        {
            // if we are using DPoP only, then we don't need/want the default
            // JwtBearerHandler to add its WWW-Authenticate response header,
            // so we have to set the status code ourselves
            context.Response.StatusCode = 401;
            context.HandleResponse();
        }
        else
        {
            if (context.HttpContext.Items.TryGetValue("Bearer-ErrorDescription", out var bearerErrorDesc) && bearerErrorDesc is string description)
            {
                context.ErrorDescription = description;
            }
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

        if (context.HttpContext.Items.TryGetValue("DPoP-Error", out var dpopErrorObj) && dpopErrorObj is string error)
        {
            sb.Append(" error=\"");
            sb.Append(error);
            sb.Append('\"');

            if (context.HttpContext.Items.TryGetValue("DPoP-ErrorDescription", out var dpopErrorDescObj) && dpopErrorDescObj is string description)
            {
                sb.Append(", error_description=\"");
                sb.Append(description);
                sb.Append('\"');
            }
        }

        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, sb.ToString());

        if (context.HttpContext.Items.TryGetValue("DPoP-Nonce", out var nonceObj) && nonceObj is string nonce)
        {
            context.Response.Headers[HttpHeaders.DPoPNonce] = nonce;
        }

        return Task.CompletedTask;
    }
}
