// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Configuration;

/// <summary>
/// Configures the authorization policy for the conformance report endpoint.
/// </summary>
internal class ConfigureConformanceReportAuthorizationPolicy
    : IConfigureOptions<AuthorizationOptions>
{
    private readonly ConformanceReportOptions _conformanceOptions;

    public ConfigureConformanceReportAuthorizationPolicy(
        IOptions<ConformanceReportOptions> conformanceOptions)
        => _conformanceOptions = conformanceOptions.Value;

    public void Configure(AuthorizationOptions options)
    {
        // Only register the policy if ConfigureAuthorization is provided
        if (_conformanceOptions.ConfigureAuthorization != null)
        {
            options.AddPolicy(
                _conformanceOptions.AuthorizationPolicyName,
                _conformanceOptions.ConfigureAuthorization);
        }
    }
}
