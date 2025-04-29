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
    /// </summary>
    /// <remarks>If the string does not contain any dots, it is treated as a 
    /// subdomain. For example, if the non-mTLS endpoints are hosted at 
    /// example.com, configuring this option with the value "mtls" means that 
    /// mtls is required for requests to mtls.example.com.
    /// 
    /// If the string contains dots, it is treated as a complete domain.
    /// mTLS will be required for requests whose host name matches the 
    /// configured domain name completely, including the port number. 
    /// This allows for separate domains for the mTLS and non-mTLS endpoints. 
    /// For example, identity.example.com and mtls.example.com.
    /// </remarks>
    public string? DomainName { get; set; }

    /// <summary>
    /// Specifies whether a cnf claim gets emitted for access tokens if a client certificate was present.
    /// Normally the cnf claims only gets emitted if the client used the client certificate for authentication,
    /// setting this to true, will set the claim regardless of the authentication method. (defaults to false).
    /// </summary>
    public bool AlwaysEmitConfirmationClaim { get; set; }
}
