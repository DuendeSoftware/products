using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static Duende.IdentityModel.OidcConstants;

namespace Api.DPoP;

public class DPoPJwtBearerEvents : JwtBearerEvents
{
    private readonly IOptionsMonitor<DPoPOptions> _optionsMonitor;
    private readonly DPoPProofValidator _validator;

    public DPoPJwtBearerEvents(IOptionsMonitor<DPoPOptions> optionsMonitor, DPoPProofValidator validator)
    {
        _optionsMonitor = optionsMonitor;
        _validator = validator;
    }

    public override Task MessageReceived(MessageReceivedContext context)
    {
        var dpopOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (context.HttpContext.Request.TryGetDPoPAccessToken(out var token))
        {
            context.Token = token;
        }
        else if (dpopOptions.Mode == DPoPMode.DPoPOnly)
        {
            // this rejects the attempt for this handler,
            // since we don't want to attempt Bearer given the Mode
            context.NoResult();
        }

        return Task.CompletedTask;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var dpopOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (context.HttpContext.Request.TryGetDPoPAccessToken(out var at))
        {
            var proofToken = context.HttpContext.Request.GetDPoPProofToken();
            var result = await _validator.ValidateAsync(new DPoPProofValidatonContext
            {
                Scheme = context.Scheme.Name,
                ProofToken = proofToken,
                AccessToken = at,
                Method = context.HttpContext.Request.Method,
                Url = context.HttpContext.Request.Scheme + "://" + context.HttpContext.Request.Host + context.HttpContext.Request.PathBase + context.HttpContext.Request.Path
            });

            if (result.IsError)
            {
                // fails the result
                context.Fail(result.ErrorDescription ?? result.Error);

                // we need to stash these values away so they are available later when the Challenge method is called later
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
        else if (dpopOptions.Mode == DPoPMode.DPoPAndBearer)
        {
            // if the scheme used was not DPoP, then it was Bearer
            // and if a access token was presented with a cnf, then the 
            // client should have sent it as DPoP, so we fail the request
            if (context.Principal?.HasClaim(x => x.Type == JwtClaimTypes.Confirmation) == true)
            {
                context.HttpContext.Items["Bearer-ErrorDescription"] = "Must use DPoP when using an access token with a 'cnf' claim";
                context.Fail("Must use DPoP when using an access token with a 'cnf' claim");
            }
        }
    }

    public override Task Challenge(JwtBearerChallengeContext context)
    {
        var dpopOptions = _optionsMonitor.Get(context.Scheme.Name);

        if (dpopOptions.Mode == DPoPMode.DPoPOnly)
        {
            // if we are using DPoP only, then we don't need/want the default
            // JwtBearerHandler to add its WWW-Authenticate response header
            // so we have to set the status code ourselves
            context.Response.StatusCode = 401;
            context.HandleResponse();
        }
        else if (context.HttpContext.Items.ContainsKey("Bearer-ErrorDescription"))
        {
            var description = context.HttpContext.Items["Bearer-ErrorDescription"] as string;
            context.ErrorDescription = description;
        }

        if (context.HttpContext.Request.IsDPoPAuthorizationScheme())
        {
            // if we are challening due to dpop, then don't allow bearer www-auth to emit an error
            context.Error = null;
        }

        // now we always want to add our WWW-Authenticate for DPoP
        // For example:
        // WWW-Authenticate: DPoP error="invalid_dpop_proof", error_description="Invalid 'iat' value."
        var sb = new StringBuilder();
        sb.Append(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP);

        if (context.HttpContext.Items.ContainsKey("DPoP-Error"))
        {
            var error = context.HttpContext.Items["DPoP-Error"] as string;
            sb.Append(" error=\"");
            sb.Append(error);
            sb.Append('\"');

            if (context.HttpContext.Items.ContainsKey("DPoP-ErrorDescription"))
            {
                var description = context.HttpContext.Items["DPoP-ErrorDescription"] as string;

                sb.Append(", error_description=\"");
                sb.Append(description);
                sb.Append('\"');
            }
        }

        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, sb.ToString());

        
        if (context.HttpContext.Items.ContainsKey("DPoP-Nonce"))
        {
            var nonce = context.HttpContext.Items["DPoP-Nonce"] as string;
            context.Response.Headers[HttpHeaders.DPoPNonce] = nonce;
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
