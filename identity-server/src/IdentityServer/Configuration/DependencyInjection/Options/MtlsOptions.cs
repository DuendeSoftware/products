// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for Mutual TLS features
/// </summary>
public class MutualTlsOptions
{
    /// <summary>
    /// Specifies if MTLS support should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Specifies the name of the authentication handler for X.509 client certificates
    /// </summary>
    public string ClientCertificateAuthenticationScheme { get; set; } = "Certificate";

    /// <summary>
    /// Specifies a separate domain to run the MTLS endpoints on.
    /// If the string does not contain any dots, a subdomain is assumed - e.g. main domain: identityserver.local, MTLS domain: mtls.identityserver.local
    /// If the string contains dots, a completely separate domain is assumend, e.g. main domain: identity.app.com, MTLS domain: mtls.app.com. In this case you must set a static issuer name on the options.
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Specifies whether a cnf claim gets emitted for access tokens if a client certificate was present.
    /// Normally the cnf claims only gets emitted if the client used the client certificate for authentication,
    /// setting this to true, will set the claim regardless of the authentication method. (defaults to false).
    /// </summary>
    public bool AlwaysEmitConfirmationClaim { get; set; }
}
