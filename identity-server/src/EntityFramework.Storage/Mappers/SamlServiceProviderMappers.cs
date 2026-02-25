// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for SAML service providers.
/// </summary>
public static class SamlServiceProviderMappers
{
    /// <summary>
    /// Maps an entity to a model.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns></returns>
    public static Models.SamlServiceProvider ToModel(this Entities.SamlServiceProvider entity) =>
        new Models.SamlServiceProvider
        {
            EntityId = entity.EntityId,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Enabled = entity.Enabled,
            ClockSkew = entity.ClockSkewTicks.HasValue
                ? TimeSpan.FromTicks(entity.ClockSkewTicks.Value) : null,
            RequestMaxAge = entity.RequestMaxAgeTicks.HasValue
                ? TimeSpan.FromTicks(entity.RequestMaxAgeTicks.Value) : null,
            AssertionConsumerServiceBinding = (SamlBinding)entity.AssertionConsumerServiceBinding,
            AssertionConsumerServiceUrls = entity.AssertionConsumerServiceUrls?
                .Select(a => new Uri(a.Url)).ToHashSet() ?? new HashSet<Uri>(),
            SingleLogoutServiceUrl = entity.SingleLogoutServiceUrl != null
                ? new SamlEndpointType
                {
                    Location = new Uri(entity.SingleLogoutServiceUrl),
                    Binding = (SamlBinding)entity.SingleLogoutServiceBinding!.Value
                } : null,
            RequireSignedAuthnRequests = entity.RequireSignedAuthnRequests,
#pragma warning disable SYSLIB0057 // Type or member is obsolete
            // TODO - Use X509CertificateLoader in a future release (when we drop NET8 support)
            SigningCertificates = entity.SigningCertificates?
                .Select(c => new X509Certificate2(Convert.FromBase64String(c.Data))).ToList(),
            EncryptionCertificates = entity.EncryptionCertificates?
                .Select(c => new X509Certificate2(Convert.FromBase64String(c.Data))).ToList(),
#pragma warning restore SYSLIB0057 // Type or member is obsolete
            EncryptAssertions = entity.EncryptAssertions,
            RequireConsent = entity.RequireConsent,
            AllowIdpInitiated = entity.AllowIdpInitiated,
            ClaimMappings = entity.ClaimMappings?
                .ToDictionary(m => m.ClaimType, m => m.SamlAttributeName)
                ?? new Dictionary<string, string>(),
            DefaultNameIdFormat = entity.DefaultNameIdFormat,
            DefaultPersistentNameIdentifierClaimType = entity.DefaultPersistentNameIdentifierClaimType,
            SigningBehavior = entity.SigningBehavior.HasValue
                ? (SamlSigningBehavior)entity.SigningBehavior.Value : null,
        };

    /// <summary>
    /// Maps a model to an entity.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns></returns>
    public static Entities.SamlServiceProvider ToEntity(this Models.SamlServiceProvider model) =>
        new Entities.SamlServiceProvider
        {
            EntityId = model.EntityId,
            DisplayName = model.DisplayName,
            Description = model.Description,
            Enabled = model.Enabled,
            ClockSkewTicks = model.ClockSkew?.Ticks,
            RequestMaxAgeTicks = model.RequestMaxAge?.Ticks,
            AssertionConsumerServiceBinding = (int)model.AssertionConsumerServiceBinding,
            AssertionConsumerServiceUrls = model.AssertionConsumerServiceUrls?
                .Select(u => new SamlServiceProviderAssertionConsumerService { Url = u.AbsoluteUri })
                .ToList() ?? new List<SamlServiceProviderAssertionConsumerService>(),
            SingleLogoutServiceUrl = model.SingleLogoutServiceUrl?.Location.AbsoluteUri,
            SingleLogoutServiceBinding = model.SingleLogoutServiceUrl != null
                ? (int)model.SingleLogoutServiceUrl.Binding : null,
            RequireSignedAuthnRequests = model.RequireSignedAuthnRequests,
            SigningCertificates = model.SigningCertificates?
                .Select(c => new SamlServiceProviderSigningCertificate
                {
                    Data = Convert.ToBase64String(c.RawData)
                }).ToList() ?? new List<SamlServiceProviderSigningCertificate>(),
            EncryptionCertificates = model.EncryptionCertificates?
                .Select(c => new SamlServiceProviderEncryptionCertificate
                {
                    Data = Convert.ToBase64String(c.RawData)
                }).ToList() ?? new List<SamlServiceProviderEncryptionCertificate>(),
            EncryptAssertions = model.EncryptAssertions,
            RequireConsent = model.RequireConsent,
            AllowIdpInitiated = model.AllowIdpInitiated,
            ClaimMappings = model.ClaimMappings?
                .Select(kvp => new SamlServiceProviderClaimMapping
                {
                    ClaimType = kvp.Key,
                    SamlAttributeName = kvp.Value
                }).ToList() ?? new List<SamlServiceProviderClaimMapping>(),
            DefaultNameIdFormat = model.DefaultNameIdFormat,
            DefaultPersistentNameIdentifierClaimType = model.DefaultPersistentNameIdentifierClaimType,
            SigningBehavior = model.SigningBehavior.HasValue
                ? (int)model.SigningBehavior.Value : null,
        };
}
