// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlCertificate
{
    public int Id { get; set; }
    public string Data { get; set; }  // Base64-encoded DER certificate
    public int Use { get; set; }  // Maps to KeyUse flags enum: 1=Signing, 2=Encryption, 3=Both

    public int SamlServiceProviderId { get; set; }
    public SamlServiceProvider SamlServiceProvider { get; set; }
}
