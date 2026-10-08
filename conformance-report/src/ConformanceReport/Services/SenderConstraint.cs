// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Shared sender-constrained token determination used by both the FAPI 2.0 and OAuth 2.1
/// assessors, so the definition of "DPoP-bound" and "mTLS-bound" cannot drift between them.
/// </summary>
internal static class SenderConstraint
{
    /// <summary>
    /// A client is DPoP-bound only if it requires DPoP and the server has at least one
    /// configured DPoP proof signing algorithm. An empty algorithm collection rejects
    /// every DPoP proof, so RequireDPoP alone is not a usable constraint.
    /// </summary>
    public static bool IsDPoPBound(ConformanceReportServerOptions options, ConformanceReportClient client) =>
        client.RequireDPoP && options.DPoPSigningAlgorithms.Count > 0;

    /// <summary>
    /// A client is mTLS-bound only if mTLS is enabled at the server level, the client is
    /// confidential, and every configured secret is an X509 thumbprint or name secret
    /// (X509CertificateBase64 is used for private_key_jwt, not mTLS).
    /// </summary>
    public static bool IsMtlsBound(ConformanceReportServerOptions options, ConformanceReportClient client) =>
        options.MutualTlsEnabled &&
        client.RequireClientSecret &&
        client.ClientSecretTypes.Count > 0 &&
        client.ClientSecretTypes.All(s =>
            s == ConformanceReportSecretTypes.X509CertificateThumbprint ||
            s == ConformanceReportSecretTypes.X509CertificateName);
}
