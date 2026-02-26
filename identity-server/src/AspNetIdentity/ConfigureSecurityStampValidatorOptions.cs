// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.AspNetIdentity;

public class ConfigureSecurityStampValidatorOptions(ISessionClaimsFilter sessionClaimsFilter, IHttpContextAccessor httpContextAccessor) : IConfigureOptions<SecurityStampValidatorOptions>
{
    public void Configure(SecurityStampValidatorOptions options) => options.OnRefreshingPrincipal = async context =>
        await SecurityStampValidatorCallback.UpdatePrincipal(context, sessionClaimsFilter, httpContextAccessor.HttpContext?.RequestAborted ?? default);
}
