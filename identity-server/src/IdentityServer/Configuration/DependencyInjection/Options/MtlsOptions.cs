// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for Mutual TLS (mTLS) support, which enables certificate-bound tokens and
/// X.509 client certificate authentication.
/// </summary>
public class MutualTlsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Mutual TLS support is enabled. When disabled, mTLS endpoints and certificate-bound token
    /// features are not available.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>.</remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the name of the ASP.NET Core authentication handler used to authenticate X.509 client
    /// certificates.
    /// </summary>
    /// <remarks>Defaults to <c>"Certificate"</c>.</remarks>
    public string ClientCertificateAuthenticationScheme { get; set; } = "Certificate";

    /// <summary>
    /// Gets or sets a subdomain or full domain name on which the mTLS protocol endpoints are hosted.
    /// When not set, mTLS endpoints use path-based routing under the main IdentityServer domain.
    /// </summary>
    /// <remarks>
    /// A value without dots (e.g., <c>"mtls"</c>) is treated as a subdomain of the main
    /// IdentityServer host. A value containing dots (e.g., <c>"identityserver-mtls.io"</c>) is
    /// treated as a fully qualified domain name. When a full domain name is used, the
    /// <c>IssuerUri</c> must also be set to a fixed value.
    /// </remarks>
    public string? DomainName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <c>cnf</c> (confirmation) claim in access tokens is emitted whenever a client certificate
    /// is present on the request, regardless of whether the certificate was used for client
    /// authentication.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When <c>false</c>, the <c>cnf</c> claim is only emitted when
    /// the client authenticated using its certificate. Set to <c>true</c> to bind tokens to the
    /// certificate even when another authentication method was used.
    /// </remarks>
    public bool AlwaysEmitConfirmationClaim { get; set; }
}
