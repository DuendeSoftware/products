// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Pre-populates <see cref="SamlAuthenticationOptions"/> from <see cref="SamlProvider"/>
/// properties, giving customers a baseline to customize.
/// </summary>
internal sealed class SamlAuthenticationConfigureOptions : ConfigureAuthenticationOptions<SamlAuthenticationOptions, SamlProvider>
{
    public SamlAuthenticationConfigureOptions(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SamlAuthenticationConfigureOptions> logger)
        : base(httpContextAccessor, logger)
    {
    }

    protected override void Configure(ConfigureAuthenticationContext<SamlAuthenticationOptions, SamlProvider> context)
    {
        var provider = context.IdentityProvider;
        var options = context.AuthenticationOptions;

        options.SpEntityId = provider.SpEntityId;
        options.OutboundSigningAlgorithm = provider.OutboundSigningAlgorithm;
        options.WantAssertionsSigned = provider.WantAssertionsSigned;
        options.SignInScheme = context.DynamicProviderOptions.SignInScheme;
        options.SignOutScheme = context.DynamicProviderOptions.SignOutScheme;
    }
}
