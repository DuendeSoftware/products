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
    extension(IdentityServerOptions options)
    {
        public ConformanceReportServerOptions ToConformanceReportServerOptions(
            bool loopbackRedirectUrisEnabled,
            IReadOnlyCollection<string> tokenSigningAlgorithms) => new()
            {
                PushedAuthorizationEndpointEnabled = options.Endpoints.EnablePushedAuthorizationEndpoint,
                PushedAuthorizationRequired = options.PushedAuthorization.Required,
                PushedAuthorizationLifetime = options.PushedAuthorization.Lifetime,
                AllowUnregisteredPushedRedirectUris = options.PushedAuthorization.AllowUnregisteredPushedRedirectUris,
                LoopbackRedirectUrisEnabled = loopbackRedirectUrisEnabled,
                DiscoveryEndpointEnabled = options.Endpoints.EnableDiscoveryEndpoint,
                MutualTlsEnabled = options.MutualTls.Enabled,
                SupportedClientAssertionSigningAlgorithms = options.SupportedClientAssertionSigningAlgorithms.ToList(),
                SupportedRequestObjectSigningAlgorithms = options.SupportedRequestObjectSigningAlgorithms.ToList(),
                DPoPSigningAlgorithms = options.DPoP.SupportedDPoPSigningAlgorithms.ToList(),
                TokenSigningAlgorithms = tokenSigningAlgorithms,
                JwtValidationClockSkew = options.JwtValidationClockSkew,
                EmitIssuerIdentificationResponseParameter = options.EmitIssuerIdentificationResponseParameter,
                IssuerUri = options.IssuerUri,
            };
    }
}
