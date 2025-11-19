// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Blazor;

/// <summary>
/// Claims transform that adds the server management claims.
/// This is right now only the logout URL, as the other management claims only make sense in the browser. 
/// </summary>
/// <param name="httpContextAccessor"></param>
/// <param name="options"></param>
internal class AddServerManagementClaimsTransform(IHttpContextAccessor httpContextAccessor, IOptionsMonitor<BffOptions> options) : IClaimsTransformation
{
    private HttpContext _httpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("not running in http context");

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var claimsIdentity = new ClaimsIdentity();
        if (!principal.HasClaim(claim => claim.Type == Constants.ClaimTypes.LogoutUrl))
        {
            var sessionId = principal.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId)?.Value;
            claimsIdentity.AddClaim(new Claim(
                Constants.ClaimTypes.LogoutUrl,
                LogoutUrlBuilder.Build(_httpContext.Request.PathBase, options.CurrentValue, sessionId).ToString()));
        }

        principal.AddIdentity(claimsIdentity);
        return Task.FromResult(principal);
    }
}
