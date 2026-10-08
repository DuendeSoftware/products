// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport;

/// <summary>
/// Represents server-level options for conformance assessment.
/// </summary>
internal sealed record ConformanceReportServerOptions
{
    public required bool PushedAuthorizationEndpointEnabled { get; init; }

    public required bool PushedAuthorizationRequired { get; init; }

    public required int PushedAuthorizationLifetime { get; init; }

    /// <summary>
    /// Whether clients may push redirect URIs via PAR that are not registered. When enabled,
    /// confidential clients may be configured without any registered redirect URIs.
    /// </summary>
    public required bool AllowUnregisteredPushedRedirectUris { get; init; }

    /// <summary>
    /// Whether the server is configured to accept http loopback redirect URIs for native
    /// clients (RFC 8252 section 7.3), e.g. with IdentityServer's StrictRedirectUriValidatorAppAuth.
    /// </summary>
    public required bool LoopbackRedirectUrisEnabled { get; init; }

    /// <summary>
    /// Whether the discovery endpoint (/.well-known/openid-configuration) is enabled.
    /// </summary>
    public required bool DiscoveryEndpointEnabled { get; init; }

    public required bool MutualTlsEnabled { get; init; }

    /// <summary>
    /// Algorithms accepted for client assertions used in private_key_jwt client authentication.
    /// An empty collection means all algorithms are accepted.
    /// </summary>
    public required IReadOnlyCollection<string> SupportedClientAssertionSigningAlgorithms { get; init; }

    /// <summary>
    /// Algorithms accepted for JAR request objects. An empty collection means all algorithms are accepted.
    /// </summary>
    public required IReadOnlyCollection<string> SupportedRequestObjectSigningAlgorithms { get; init; }

    public required IReadOnlyCollection<string> DPoPSigningAlgorithms { get; init; }

    /// <summary>
    /// Algorithms used by the server to sign the tokens it issues.
    /// </summary>
    public required IReadOnlyCollection<string> TokenSigningAlgorithms { get; init; }

    public required TimeSpan JwtValidationClockSkew { get; init; }

    public required bool EmitIssuerIdentificationResponseParameter { get; init; }

    /// <summary>
    /// The explicitly configured issuer URI, if any. When null, the issuer is inferred from the request.
    /// </summary>
    public string? IssuerUri { get; init; }
}
