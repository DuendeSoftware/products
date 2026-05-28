// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

#pragma warning disable IDE0005 // Roslyn false-positive: all usings below are required
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.Models;
#pragma warning restore IDE0005

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for SAML Service Providers.
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
            ClockSkew = entity.ClockSkewSeconds.HasValue
                ? TimeSpan.FromSeconds(entity.ClockSkewSeconds.Value)
                : null,
            RequestMaxAge = entity.RequestMaxAgeSeconds.HasValue
                ? TimeSpan.FromSeconds(entity.RequestMaxAgeSeconds.Value)
                : null,
            AssertionLifetime = entity.AssertionLifetimeSeconds.HasValue
                ? TimeSpan.FromSeconds(entity.AssertionLifetimeSeconds.Value)
                : null,
            AssertionConsumerServiceUrls = entity.AssertionConsumerServiceUrls?
                .Select(u => new IndexedEndpoint
                {
                    Location = u.Location,
                    Binding = ParseBinding(u.Binding),
                    Index = u.Index,
                    IsDefault = u.IsDefault
                })
                .ToHashSet() ?? new HashSet<IndexedEndpoint>(),
            SingleLogoutServiceUrls = entity.SingleLogoutServiceUrls?
                .Select(u => new SamlEndpointType
                {
                    Location = u.Location,
                    Binding = ParseBinding(u.Binding)
                })
                .ToHashSet() ?? new HashSet<SamlEndpointType>(),
            RequireSignedAuthnRequests = entity.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = entity.RequireSignedLogoutResponses,
            Certificates = entity.Certificates?
                .Select(c => new ServiceProviderCertificate
                {
                    Certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(c.Data)),
                    Use = (KeyUse)c.Use
                })
                .ToList(),
            AllowIdpInitiated = entity.AllowIdpInitiated,
            ClaimMappings = entity.ClaimMappings?
                .ToDictionary(m => m.ClaimType, m => m.SamlAttributeName)
                ?? new Dictionary<string, string>(),
            AuthnContextMappings = entity.AuthnContextMappings?
                .ToDictionary(m => m.OidcValue, m => m.SamlAuthnContextClassRef)
                ?? new Dictionary<string, string>(),
            AllowedScopes = entity.AllowedScopes?
                .Select(s => s.Scope)
                .ToHashSet()
                ?? new HashSet<string>(),
            RequestedClaimTypes = entity.RequestedClaimTypes?
                .Select(r => r.ClaimType)
                .ToList()
                ?? [],
            DefaultNameIdFormat = entity.DefaultNameIdFormat,
            EmailNameIdClaimType = entity.EmailNameIdClaimType,
            AllowedSignatureAlgorithms = entity.AllowedSignatureAlgorithms,
            SigningBehavior = entity.SigningBehavior.HasValue
                ? (SamlSigningBehavior?)entity.SigningBehavior.Value
                : null,
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
            ClockSkewSeconds = model.ClockSkew?.TotalSeconds,
            RequestMaxAgeSeconds = model.RequestMaxAge?.TotalSeconds,
            AssertionLifetimeSeconds = model.AssertionLifetime?.TotalSeconds,
            AssertionConsumerServiceUrls = model.AssertionConsumerServiceUrls?
                .Select(u => new Entities.SamlAssertionConsumerService
                {
                    Location = u.Location,
                    Binding = BindingToUrn(u.Binding),
                    Index = u.Index,
                    IsDefault = u.IsDefault
                })
                .ToList() ?? new List<Entities.SamlAssertionConsumerService>(),
            SingleLogoutServiceUrls = model.SingleLogoutServiceUrls?
                .Select(u => new Entities.SamlSingleLogoutService
                {
                    Location = u.Location,
                    Binding = BindingToUrn(u.Binding)
                })
                .ToList() ?? new List<Entities.SamlSingleLogoutService>(),
            RequireSignedAuthnRequests = model.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = model.RequireSignedLogoutResponses,
            Certificates = model.Certificates?
                .Select(c => new Entities.SamlCertificate
                {
                    Data = Convert.ToBase64String(c.Certificate.Export(X509ContentType.Cert)),
                    Use = (int)c.Use
                })
                .ToList() ?? [],
            AllowIdpInitiated = model.AllowIdpInitiated,
            ClaimMappings = model.ClaimMappings?
                .Select(pair => new Entities.SamlClaimMapping
                {
                    ClaimType = pair.Key,
                    SamlAttributeName = pair.Value
                })
                .ToList() ?? new List<Entities.SamlClaimMapping>(),
            AuthnContextMappings = model.AuthnContextMappings?
                .Select(pair => new Entities.SamlAuthnContextMapping
                {
                    OidcValue = pair.Key,
                    SamlAuthnContextClassRef = pair.Value
                })
                .ToList() ?? [],
            AllowedScopes = model.AllowedScopes?
                .Select(scope => new Entities.SamlAllowedScope
                {
                    Scope = scope
                })
                .ToList() ?? new List<Entities.SamlAllowedScope>(),
            RequestedClaimTypes = model.RequestedClaimTypes?
                .Select(ct => new Entities.SamlRequestedClaimType
                {
                    ClaimType = ct
                })
                .ToList() ?? [],
            DefaultNameIdFormat = model.DefaultNameIdFormat,
            EmailNameIdClaimType = model.EmailNameIdClaimType,
            AllowedSignatureAlgorithms = model.AllowedSignatureAlgorithms ?? new List<string>(),
            SigningBehavior = model.SigningBehavior.HasValue
                ? (int?)model.SigningBehavior.Value
                : null,
        };

    private static SamlBinding ParseBinding(string binding) => binding switch
    {
        "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" => SamlBinding.HttpRedirect,
        "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST" => SamlBinding.HttpPost,
        _ => throw new InvalidOperationException($"Unsupported SAML binding: {binding}")
    };

    private static string BindingToUrn(SamlBinding binding) => binding switch
    {
        SamlBinding.HttpRedirect => "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect",
        SamlBinding.HttpPost => "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST",
        _ => throw new InvalidOperationException($"Unsupported SAML binding: {binding}")
    };
}
