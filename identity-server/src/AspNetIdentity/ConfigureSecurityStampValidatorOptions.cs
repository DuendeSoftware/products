// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.AspNetIdentity;

public class ConfigureSecurityStampValidatorOptions(ISessionClaimsFilter sessionClaimsFilter) : IConfigureOptions<SecurityStampValidatorOptions>
{
    public void Configure(SecurityStampValidatorOptions options) => options.OnRefreshingPrincipal = async context => await SecurityStampValidatorCallback.UpdatePrincipal(context, sessionClaimsFilter);
}
