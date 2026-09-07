// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Stores.Storage.SamlServiceProviders;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SamlServiceProviderAdmin(
    SamlServiceProviderRepository repository,
    ISamlServiceProviderConfigurationValidator validator,
    ISchemaStore schemaStore) : ISamlServiceProviderAdmin
{
    // === CRUD ===

    public async Task<SaveResult<SamlServiceProviderId>> CreateAsync(CreateSamlServiceProvider serviceProvider, Ct ct)
    {
        var input = SamlSpInput.From(serviceProvider);

        var structuralError = ValidateStructure(input);
        if (structuralError is not null)
        {
            return SaveResult.Failure<SamlServiceProviderId>(structuralError);
        }

        var validationError = await RunValidatorAsync(input, ct);
        if (validationError is not null)
        {
            return SaveResult.Failure<SamlServiceProviderId>(validationError);
        }

        var extError = await ValidateExtendedPropertiesAsync(input, ct);
        if (extError is not null)
        {
            return SaveResult.Failure<SamlServiceProviderId>(extError);
        }

        var id = UuidV7.New();
        var dso = MapToDso(id.Value, input, forceNewCertIds: true);

        var result = await repository.CreateAsync(id, dso, ct);

        return result switch
        {
            CreateResult.Success => SaveResult.Success<SamlServiceProviderId>(id.Value, (DataVersion)1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                SaveResult.Failure<SamlServiceProviderId>(StorageError.AlreadyExists("samlServiceProvider", serviceProvider.EntityId)),
            _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
        };
    }

    public async Task<GetResult<SamlServiceProviderConfiguration>> GetAsync(SamlServiceProviderId id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id.Value, ct);
        if (result is null)
        {
            return GetResult.NotFound<SamlServiceProviderConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.SamlServiceProvider, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
    }

    public async Task<GetResult<SamlServiceProviderConfiguration>> GetByEntityIdAsync(string entityId, Ct ct)
    {
        var result = await repository.TryReadByEntityIdAsync(entityId, ct);
        if (result is null)
        {
            return GetResult.NotFound<SamlServiceProviderConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.SamlServiceProvider, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
    }

    public async Task<SaveResult<SamlServiceProviderId>> UpdateAsync(SamlServiceProviderId id, UpdateSamlServiceProvider serviceProvider, DataVersion expectedVersion, Ct ct)
    {
        var input = SamlSpInput.From(serviceProvider);

        var structuralError = ValidateStructure(input);
        if (structuralError is not null)
        {
            return SaveResult.Failure<SamlServiceProviderId>(structuralError);
        }

        var idValue = id.Value;
        var existing = await repository.TryReadByIdAsync(idValue, ct);
        if (existing is null)
        {
            return SaveResult.Failure<SamlServiceProviderId>(StorageError.NotFound("samlServiceProvider", idValue.ToString()));
        }

        var validationError = await RunValidatorAsync(input, ct);
        if (validationError is not null)
        {
            return SaveResult.Failure<SamlServiceProviderId>(validationError);
        }

        var extError = await ValidateExtendedPropertiesAsync(input, ct);
        if (extError is not null)
        {
            return SaveResult.Failure<SamlServiceProviderId>(extError);
        }

        var dso = MapToDso(idValue, input);

        var result = await repository.UpdateAsync(UuidV7.From(idValue), dso, expectedVersion.Value, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success<SamlServiceProviderId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
            UpdateResult.UnexpectedVersion => SaveResult.Failure<SamlServiceProviderId>(StorageError.VersionConflict()),
            UpdateResult.DoesNotExist => SaveResult.Failure<SamlServiceProviderId>(StorageError.NotFound("samlServiceProvider", idValue.ToString())),
            UpdateResult.KeyConflict => SaveResult.Failure<SamlServiceProviderId>(StorageError.AlreadyExists("samlServiceProvider", serviceProvider.EntityId)),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<SamlServiceProviderId>> DeleteAsync(SamlServiceProviderId id, Ct ct)
    {
        var idValue = id.Value;
        var result = await repository.DeleteAsync(idValue, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success<SamlServiceProviderId>(idValue, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<Duende.Storage.Querying.QueryResult<SamlServiceProviderListItem>> QueryAsync(QueryRequest<SamlServiceProviderFilter, SamlServiceProviderSortField> request, Ct ct)
    {
        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    // === Common input shape shared by Create/Update ===

    private sealed record SamlSpInput
    {
        public required string EntityId { get; init; }
        public bool Enabled { get; init; } = true;
        public string? DisplayName { get; init; }
        public string? Description { get; init; }
        public TimeSpan? ClockSkew { get; init; }
        public TimeSpan? RequestMaxAge { get; init; }
        public TimeSpan? AssertionLifetime { get; init; }
        public List<SamlIndexedEndpointConfiguration> AssertionConsumerServiceUrls { get; init; } = [];
        public List<SamlEndpointConfiguration> SingleLogoutServiceUrls { get; init; } = [];
        public bool? RequireSignedAuthnRequests { get; init; }
        public bool? RequireSignedLogoutResponses { get; init; }
        public List<SamlCertificateInput> Certificates { get; init; } = [];
        public bool AllowIdpInitiated { get; init; }
        public List<string> AllowedScopes { get; init; } = [];
        public Dictionary<string, string> ClaimMappings { get; init; } = [];
        public Dictionary<string, string> AuthnContextMappings { get; init; } = [];
        public List<string> RequestedClaimTypes { get; init; } = [];
        public string? DefaultNameIdFormat { get; init; }
        public string? EmailNameIdClaimType { get; init; }
        public SamlSigningBehavior? SigningBehavior { get; init; }
        public List<string> AllowedSignatureAlgorithms { get; init; } = [];
        public AttributeValueCollection ExtendedProperties { get; init; } = new();

        public static SamlSpInput From(CreateSamlServiceProvider sp) => new()
        {
            EntityId = sp.EntityId,
            Enabled = sp.Enabled,
            DisplayName = sp.DisplayName,
            Description = sp.Description,
            ClockSkew = sp.ClockSkew,
            RequestMaxAge = sp.RequestMaxAge,
            AssertionLifetime = sp.AssertionLifetime,
            AssertionConsumerServiceUrls = sp.AssertionConsumerServiceUrls,
            SingleLogoutServiceUrls = sp.SingleLogoutServiceUrls,
            RequireSignedAuthnRequests = sp.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = sp.RequireSignedLogoutResponses,
            Certificates = sp.Certificates,
            AllowIdpInitiated = sp.AllowIdpInitiated,
            AllowedScopes = sp.AllowedScopes,
            ClaimMappings = sp.ClaimMappings,
            AuthnContextMappings = sp.AuthnContextMappings,
            RequestedClaimTypes = sp.RequestedClaimTypes,
            DefaultNameIdFormat = sp.DefaultNameIdFormat,
            EmailNameIdClaimType = sp.EmailNameIdClaimType,
            SigningBehavior = sp.SigningBehavior,
            AllowedSignatureAlgorithms = sp.AllowedSignatureAlgorithms,
            ExtendedProperties = sp.ExtendedProperties
        };

        public static SamlSpInput From(UpdateSamlServiceProvider sp) => new()
        {
            EntityId = sp.EntityId,
            Enabled = sp.Enabled,
            DisplayName = sp.DisplayName,
            Description = sp.Description,
            ClockSkew = sp.ClockSkew,
            RequestMaxAge = sp.RequestMaxAge,
            AssertionLifetime = sp.AssertionLifetime,
            AssertionConsumerServiceUrls = sp.AssertionConsumerServiceUrls,
            SingleLogoutServiceUrls = sp.SingleLogoutServiceUrls,
            RequireSignedAuthnRequests = sp.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = sp.RequireSignedLogoutResponses,
            Certificates = sp.Certificates,
            AllowIdpInitiated = sp.AllowIdpInitiated,
            AllowedScopes = sp.AllowedScopes,
            ClaimMappings = sp.ClaimMappings,
            AuthnContextMappings = sp.AuthnContextMappings,
            RequestedClaimTypes = sp.RequestedClaimTypes,
            DefaultNameIdFormat = sp.DefaultNameIdFormat,
            EmailNameIdClaimType = sp.EmailNameIdClaimType,
            SigningBehavior = sp.SigningBehavior,
            AllowedSignatureAlgorithms = sp.AllowedSignatureAlgorithms,
            ExtendedProperties = sp.ExtendedProperties
        };
    }

    // === Structural Validation ===

    private static StorageError? ValidateStructure(SamlSpInput sp)
    {
        if (string.IsNullOrWhiteSpace(sp.EntityId))
        {
            return StorageError.Required("EntityId");
        }

        if (sp.DisplayName is not null && string.IsNullOrWhiteSpace(sp.DisplayName))
        {
            return StorageError.InvalidValue("DisplayName", "Display name must not be empty or whitespace.");
        }

        // Validate ACS URLs
        if (sp.AssertionConsumerServiceUrls is not null)
        {
            foreach (var acs in sp.AssertionConsumerServiceUrls)
            {
                if (acs is null)
                {
                    return StorageError.InvalidValue("AssertionConsumerServiceUrls", "ACS endpoint list must not contain null entries.");
                }

                if (string.IsNullOrWhiteSpace(acs.Location))
                {
                    return StorageError.InvalidValue("AssertionConsumerServiceUrls", "ACS endpoint location must not be empty.");
                }

                if (!Uri.TryCreate(acs.Location, UriKind.Absolute, out _))
                {
                    return StorageError.InvalidValue("AssertionConsumerServiceUrls", $"ACS endpoint location '{acs.Location}' is not a valid absolute URI.");
                }
            }

            // Unique index values
            var indices = sp.AssertionConsumerServiceUrls.Select(a => a.Index).ToList();
            if (indices.Count != indices.Distinct().Count())
            {
                return StorageError.InvalidValue("AssertionConsumerServiceUrls", "ACS endpoint list contains duplicate Index values.");
            }
        }

        // Validate SLO URLs
        if (sp.SingleLogoutServiceUrls is not null)
        {
            foreach (var slo in sp.SingleLogoutServiceUrls)
            {
                if (slo is null)
                {
                    return StorageError.InvalidValue("SingleLogoutServiceUrls", "SLO endpoint list must not contain null entries.");
                }

                if (string.IsNullOrWhiteSpace(slo.Location))
                {
                    return StorageError.InvalidValue("SingleLogoutServiceUrls", "SLO endpoint location must not be empty.");
                }

                if (!Uri.TryCreate(slo.Location, UriKind.Absolute, out _))
                {
                    return StorageError.InvalidValue("SingleLogoutServiceUrls", $"SLO endpoint location '{slo.Location}' is not a valid absolute URI.");
                }
            }
        }

        // Validate certificates
        if (sp.Certificates is not null)
        {
            foreach (var cert in sp.Certificates)
            {
                if (cert is null)
                {
                    return StorageError.InvalidValue("Certificates", "Certificate list must not contain null entries.");
                }

                if (string.IsNullOrWhiteSpace(cert.Base64Data))
                {
                    return StorageError.InvalidValue("Certificates", "Certificate Base64Data must not be empty.");
                }

                try
                {
                    var bytes = Convert.FromBase64String(cert.Base64Data);
                    using var x509 = X509CertificateLoader.LoadCertificate(bytes);
                }
                catch (FormatException)
                {
                    return StorageError.InvalidValue("Certificates", "Certificate Base64Data is not valid base64.");
                }
                catch (CryptographicException)
                {
                    return StorageError.InvalidValue("Certificates", "Certificate Base64Data does not contain a valid X.509 certificate.");
                }
            }

            // Unique IDs (only for non-default/new certs that have IDs)
            var certIds = sp.Certificates.Where(c => c.Id != Guid.Empty).Select(c => c.Id).ToList();
            if (certIds.Count != certIds.Distinct().Count())
            {
                return StorageError.InvalidValue("Certificates", "Certificate list contains duplicate IDs.");
            }
        }

        // Validate allowed scopes
        if (sp.AllowedScopes is not null)
        {
            foreach (var scope in sp.AllowedScopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    return StorageError.InvalidValue("AllowedScopes", "Scope must not be null or whitespace.");
                }
            }
        }

        return null;
    }

    // === Validator Pipeline ===

    private async Task<StorageError?> ValidateExtendedPropertiesAsync(SamlSpInput sp, Ct ct)
    {
        if (sp.ExtendedProperties.Count == 0)
        {
            return null;
        }

        var schemaId = SchemaId.SamlServiceProvider;
        var schema = await schemaStore.GetAsync(schemaId, ct);

        if (!sp.ExtendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return StorageError.ValidationFailed(string.Join("; ", errors));
        }

        return null;
    }

    private async Task<StorageError?> RunValidatorAsync(SamlSpInput sp, Ct ct)
    {
        var model = MapToModel(sp);
        try
        {
            var context = new SamlServiceProviderConfigurationValidationContext(model);
            await validator.ValidateAsync(context, ct);

            return context.IsValid ? null : StorageError.ValidationFailed(context.ErrorMessage!);
        }
        finally
        {
            // Dispose X509Certificate2 handles created for the validator model
            if (model.Certificates is not null)
            {
                foreach (var spCert in model.Certificates)
                {
                    spCert.Certificate.Dispose();
                }
            }
        }
    }

    // === Mapping: Input → Domain Model (for validator) ===

    private static SamlServiceProvider MapToModel(SamlSpInput sp) =>
        new()
        {
            EntityId = sp.EntityId,
            Enabled = sp.Enabled,
            DisplayName = sp.DisplayName,
            Description = sp.Description,
            ClockSkew = sp.ClockSkew,
            RequestMaxAge = sp.RequestMaxAge,
            AssertionLifetime = sp.AssertionLifetime,
            RequireSignedAuthnRequests = sp.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = sp.RequireSignedLogoutResponses,
            AllowIdpInitiated = sp.AllowIdpInitiated,
            DefaultNameIdFormat = sp.DefaultNameIdFormat,
            EmailNameIdClaimType = sp.EmailNameIdClaimType,
            SigningBehavior = sp.SigningBehavior,
            AllowedSignatureAlgorithms = sp.AllowedSignatureAlgorithms,
            AllowedScopes = new HashSet<string>(sp.AllowedScopes ?? []),
            ClaimMappings = sp.ClaimMappings != null
                ? new Dictionary<string, string>(sp.ClaimMappings)
                : new Dictionary<string, string>(),
            AuthnContextMappings = sp.AuthnContextMappings != null
                ? new Dictionary<string, string>(sp.AuthnContextMappings)
                : new Dictionary<string, string>(),
            RequestedClaimTypes = new List<string>(sp.RequestedClaimTypes ?? []),
            AssertionConsumerServiceUrls = sp.AssertionConsumerServiceUrls?
                .Select(a => new IndexedEndpoint
                {
                    Location = a.Location,
                    Binding = a.Binding,
                    Index = a.Index,
                    IsDefault = a.IsDefault
                })
                .ToHashSet() ?? [],
            SingleLogoutServiceUrls = sp.SingleLogoutServiceUrls?
                .Select(s => new SamlEndpointType
                {
                    Location = s.Location,
                    Binding = s.Binding
                })
                .ToHashSet() ?? [],
            Certificates = sp.Certificates?
                .Select(c =>
                {
                    var bytes = Convert.FromBase64String(c.Base64Data);
                    var x509 = X509CertificateLoader.LoadCertificate(bytes);
                    return new ServiceProviderCertificate
                    {
                        Certificate = x509,
                        Use = c.Use
                    };
                })
                .ToList()
        };

    // === Mapping: Input → DSO ===

    private static SamlServiceProviderDso.V1 MapToDso(Guid id, SamlSpInput sp, bool forceNewCertIds = false) =>
        new()
        {
            Id = id,
            EntityId = sp.EntityId,
            Enabled = sp.Enabled,

            // Display
            DisplayName = sp.DisplayName,
            Description = sp.Description,

            // Timing
            ClockSkewTicks = sp.ClockSkew?.Ticks,
            RequestMaxAgeTicks = sp.RequestMaxAge?.Ticks,
            AssertionLifetimeTicks = sp.AssertionLifetime?.Ticks,

            // Endpoints
            AssertionConsumerServiceUrls = sp.AssertionConsumerServiceUrls?
                .Select(a => new SamlServiceProviderDso.IndexedEndpointDso(
                    a.Location, (int)a.Binding, a.Index, a.IsDefault))
                .ToList() ?? [],
            SingleLogoutServiceUrls = sp.SingleLogoutServiceUrls?
                .Select(s => new SamlServiceProviderDso.EndpointDso(s.Location, (int)s.Binding))
                .ToList() ?? [],

            // Security
            RequireSignedAuthnRequests = sp.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = sp.RequireSignedLogoutResponses,
            Certificates = NormalizeCertificates(sp.Certificates, forceNewCertIds),

            // SSO
            AllowIdpInitiated = sp.AllowIdpInitiated,

            // Scopes
            AllowedScopes = sp.AllowedScopes?.AsReadOnly() ?? [],

            // Claims
            ClaimMappings = sp.ClaimMappings != null
                ? new Dictionary<string, string>(sp.ClaimMappings)
                : new Dictionary<string, string>(),
            AuthnContextMappings = sp.AuthnContextMappings != null
                ? new Dictionary<string, string>(sp.AuthnContextMappings)
                : new Dictionary<string, string>(),
            RequestedClaimTypes = sp.RequestedClaimTypes?.AsReadOnly() ?? [],

            // NameID
            DefaultNameIdFormat = sp.DefaultNameIdFormat,
            EmailNameIdClaimType = sp.EmailNameIdClaimType,

            // Signing
            SigningBehavior = sp.SigningBehavior.HasValue ? (int)sp.SigningBehavior.Value : null,
            AllowedSignatureAlgorithms = sp.AllowedSignatureAlgorithms?.AsReadOnly() ?? [],

            // Extended properties
            ExtendedAttributeValues = EavMapper.ToDsoList(sp.ExtendedProperties)
        };

    private static List<SamlServiceProviderDso.CertificateDso> NormalizeCertificates(
        List<SamlCertificateInput>? certificates, bool forceNewIds = false)
    {
        if (certificates is null || certificates.Count == 0)
        {
            return [];
        }

        return certificates.Select(c =>
        {
            // Always assign new IDs on create; on update, preserve existing IDs
            var certId = forceNewIds || c.Id == Guid.Empty ? UuidV7.New().Value : c.Id;

            // Normalize: re-export as DER to strip any private key material
            var rawBytes = Convert.FromBase64String(c.Base64Data);
            using var x509 = X509CertificateLoader.LoadCertificate(rawBytes);
            var normalizedBytes = x509.Export(X509ContentType.Cert);
            var normalizedBase64 = Convert.ToBase64String(normalizedBytes);

            return new SamlServiceProviderDso.CertificateDso(certId, normalizedBase64, (int)c.Use);
        }).ToList();
    }

    // === Mapping: DSO → Configuration ===

    private static SamlServiceProviderConfiguration MapToConfiguration(SamlServiceProviderDso.V1 dso, IReadOnlyAttributeSchema? schema) =>
        new()
        {
            EntityId = dso.EntityId,
            Enabled = dso.Enabled,

            // Display
            DisplayName = dso.DisplayName,
            Description = dso.Description,

            // Timing
            ClockSkew = dso.ClockSkewTicks.HasValue ? TimeSpan.FromTicks(dso.ClockSkewTicks.Value) : null,
            RequestMaxAge = dso.RequestMaxAgeTicks.HasValue ? TimeSpan.FromTicks(dso.RequestMaxAgeTicks.Value) : null,
            AssertionLifetime = dso.AssertionLifetimeTicks.HasValue ? TimeSpan.FromTicks(dso.AssertionLifetimeTicks.Value) : null,

            // Endpoints
            AssertionConsumerServiceUrls = dso.AssertionConsumerServiceUrls
                .Select(a => new SamlIndexedEndpointConfiguration
                {
                    Location = a.Location,
                    Binding = (SamlBinding)a.Binding,
                    Index = a.Index,
                    IsDefault = a.IsDefault
                })
                .ToList(),
            SingleLogoutServiceUrls = dso.SingleLogoutServiceUrls
                .Select(s => new SamlEndpointConfiguration
                {
                    Location = s.Location,
                    Binding = (SamlBinding)s.Binding
                })
                .ToList(),

            // Security
            RequireSignedAuthnRequests = dso.RequireSignedAuthnRequests,
            RequireSignedLogoutResponses = dso.RequireSignedLogoutResponses,
            Certificates = dso.Certificates
                .Select(c =>
                {
                    // Populate read-only metadata from the stored certificate bytes
                    var bytes = Convert.FromBase64String(c.Base64Data);
                    using var x509 = X509CertificateLoader.LoadCertificate(bytes);

                    return new SamlCertificateConfiguration
                    {
                        Id = c.Id,
                        Base64Data = c.Base64Data,
                        Use = (KeyUse)c.Use,
                        Subject = x509.Subject,
                        Thumbprint = x509.Thumbprint,
                        NotAfter = x509.NotAfter
                    };
                })
                .ToList(),

            // SSO
            AllowIdpInitiated = dso.AllowIdpInitiated,

            // Scopes
            AllowedScopes = [.. dso.AllowedScopes],

            // Claims
            ClaimMappings = new Dictionary<string, string>(dso.ClaimMappings),
            AuthnContextMappings = new Dictionary<string, string>(dso.AuthnContextMappings),
            RequestedClaimTypes = [.. dso.RequestedClaimTypes],

            // NameID
            DefaultNameIdFormat = dso.DefaultNameIdFormat,
            EmailNameIdClaimType = dso.EmailNameIdClaimType,

            // Signing
            SigningBehavior = dso.SigningBehavior.HasValue ? (SamlSigningBehavior)dso.SigningBehavior.Value : null,
            AllowedSignatureAlgorithms = [.. dso.AllowedSignatureAlgorithms],

            // Extended properties
            ExtendedProperties = EavMapper.ToAttributeValues(dso.ExtendedAttributeValues ?? [], schema).ToList()
        };

    private static SamlServiceProviderListItem MapToListItem(SamlServiceProviderDso.V1 dso) =>
        new()
        {
            Id = dso.Id,
            EntityId = dso.EntityId,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Description = dso.Description,
            CertificateCount = dso.Certificates.Count,
            AllowedScopeCount = dso.AllowedScopes.Count
        };
}
