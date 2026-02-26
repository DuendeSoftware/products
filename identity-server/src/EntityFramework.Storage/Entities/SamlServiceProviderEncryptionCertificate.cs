// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlServiceProviderEncryptionCertificate
{
    public int Id { get; set; }
    /// <summary>
    /// Base64-encoded DER (raw bytes) of the X.509 certificate.
    /// </summary>
    public string Data { get; set; }
    public int SamlServiceProviderId { get; set; }
    public SamlServiceProvider SamlServiceProvider { get; set; }
}
