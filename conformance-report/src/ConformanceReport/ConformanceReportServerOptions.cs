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

    public required bool MutualTlsEnabled { get; init; }

    public required IReadOnlyCollection<string> SupportedSigningAlgorithms { get; init; }

    public required TimeSpan JwtValidationClockSkew { get; init; }

    public required bool EmitIssuerIdentificationResponseParameter { get; init; }

    public required bool UseHttp303Redirects { get; init; }
}
