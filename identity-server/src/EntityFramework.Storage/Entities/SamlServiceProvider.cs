// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlServiceProvider
{
    public int Id { get; set; }
    public string EntityId { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public bool Enabled { get; set; } = true;
    public double? ClockSkewSeconds { get; set; }
    public double? RequestMaxAgeSeconds { get; set; }
    public double? AssertionLifetimeSeconds { get; set; }
    public bool? RequireSignedAuthnRequests { get; set; }
    public bool? RequireSignedLogoutResponses { get; set; }
    public bool AllowIdpInitiated { get; set; }
    public string DefaultNameIdFormat { get; set; }
    public string EmailNameIdClaimType { get; set; }
    public int? SigningBehavior { get; set; }

    public List<SamlAssertionConsumerService> AssertionConsumerServiceUrls { get; set; }
    public List<SamlSingleLogoutService> SingleLogoutServiceUrls { get; set; }
    public List<SamlCertificate> Certificates { get; set; }
    public List<SamlClaimMapping> ClaimMappings { get; set; }
    public List<SamlAuthnContextMapping> AuthnContextMappings { get; set; }
    public List<SamlAllowedScope> AllowedScopes { get; set; }
    public List<SamlRequestedClaimType> RequestedClaimTypes { get; set; }
    public List<string> AllowedSignatureAlgorithms { get; set; }

    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Updated { get; set; }
    public DateTime? LastAccessed { get; set; }
    public bool NonEditable { get; set; }
}
