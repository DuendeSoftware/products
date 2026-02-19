// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport;
using Duende.IdentityServer.Configuration;

namespace Duende.IdentityServer.ConformanceReport;

/// <summary>
/// Adapts IdentityServerOptions to ConformanceReportServerOptions.
/// </summary>
internal static class ServerOptionsAdapter
{
    public static ConformanceReportServerOptions ToConformanceReportServerOptions(
        this IdentityServerOptions options) => new()
        {
            PushedAuthorizationEndpointEnabled = options.Endpoints.EnablePushedAuthorizationEndpoint,
            PushedAuthorizationRequired = options.PushedAuthorization.Required,
            PushedAuthorizationLifetime = options.PushedAuthorization.Lifetime,
            MutualTlsEnabled = options.MutualTls.Enabled,
            SupportedSigningAlgorithms = options.SupportedClientAssertionSigningAlgorithms.ToList(),
            JwtValidationClockSkew = options.JwtValidationClockSkew,
            EmitIssuerIdentificationResponseParameter = options.EmitIssuerIdentificationResponseParameter,
            UseHttp303Redirects = true, // IdentityServer always uses HTTP 303 for redirects
        };
}
