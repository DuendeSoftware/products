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

    // TimeSpan? stored as ticks
    public long? ClockSkewTicks { get; set; }
    public long? RequestMaxAgeTicks { get; set; }

    // ACS binding (enum stored as int)
    public int AssertionConsumerServiceBinding { get; set; }

    // SLO endpoint (flattened from SamlEndpointType?)
    public string SingleLogoutServiceUrl { get; set; }
    public int? SingleLogoutServiceBinding { get; set; }

    public bool RequireSignedAuthnRequests { get; set; }
    public bool EncryptAssertions { get; set; }
    public bool RequireConsent { get; set; }
    public bool AllowIdpInitiated { get; set; }

    public string DefaultNameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";
    public string DefaultPersistentNameIdentifierClaimType { get; set; }

    // SamlSigningBehavior? stored as int?
    public int? SigningBehavior { get; set; }

    // Navigation properties
    public List<SamlServiceProviderAssertionConsumerService> AssertionConsumerServiceUrls { get; set; }
    public List<SamlServiceProviderSigningCertificate> SigningCertificates { get; set; }
    public List<SamlServiceProviderEncryptionCertificate> EncryptionCertificates { get; set; }
    public List<SamlServiceProviderClaimMapping> ClaimMappings { get; set; }

    // Audit fields (matching Client entity pattern)
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Updated { get; set; }
    public DateTime? LastAccessed { get; set; }
    public bool NonEditable { get; set; }
}
