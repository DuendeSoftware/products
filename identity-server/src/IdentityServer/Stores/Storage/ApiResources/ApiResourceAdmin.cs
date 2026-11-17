// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.Storage.Internal.Operations;
using SecretHashAlgorithm = Duende.IdentityServer.Admin.SecretHashAlgorithm;

namespace Duende.IdentityServer.Stores.Storage.ApiResources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiResourceAdmin(ApiResourceRepository repository, ApiScopeRepository scopeRepository, ISchemaStore schemaStore) : IApiResourceAdmin
{
    public async Task<SaveResult<ApiResourceId>> CreateAsync(CreateApiResource resource, Ct ct)
    {
        var structuralError = ValidateStructure(resource.Name, resource.UserClaims, resource.Scopes, resource.AllowedAccessTokenSigningAlgorithms);
        if (structuralError is not null)
        {
            return SaveResult.Failure<ApiResourceId>(structuralError);
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(resource.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<ApiResourceId>(extendedPropertiesError);
        }

        var (scopes, scopeError) = await ResolveScopesAsync(resource.Scopes, ct);
        if (scopeError is not null)
        {
            return SaveResult.Failure<ApiResourceId>(scopeError);
        }

        var id = Duende.Storage.UuidV7.New();
        var dso = MapToDso(id.Value, resource.Name, resource.Enabled, resource.DisplayName, resource.Description, resource.ShowInDiscoveryDocument, resource.RequireResourceIndicator, resource.UserClaims, resource.AllowedAccessTokenSigningAlgorithms, resource.ExtendedProperties, existingSecrets: null);

        var outcome = await repository.CreateAsync(id, dso, scopes, ct);
        return outcome.Kind switch
        {
            ApiResourceBatchOutcomeKind.Success => SaveResult.Success<ApiResourceId>(id.Value, (DataVersion)(1)),
            ApiResourceBatchOutcomeKind.AlreadyExists or ApiResourceBatchOutcomeKind.KeyConflict =>
                SaveResult.Failure<ApiResourceId>(StorageError.AlreadyExists("api_resource", resource.Name)),
            ApiResourceBatchOutcomeKind.ScopeDoesNotExist =>
                SaveResult.Failure<ApiResourceId>(StorageError.InvalidValue("Scopes", $"Scope '{outcome.ScopeName}' does not exist.")),
            _ => throw new InvalidOperationException($"Unexpected ApiResourceBatchOutcome: {outcome.Kind}")
        };
    }

    public async Task<GetResult<ApiResourceConfiguration>> GetAsync(ApiResourceId id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id.Value, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        var scopeNames = await GetScopeNamesAsync(dso.Id, ct);
        var schema = await schemaStore.GetAsync(SchemaId.ApiResource, ct);
        return GetResult.Found(MapToConfiguration(dso, scopeNames, schema), (DataVersion)(version));
    }

    public async Task<GetResult<ApiResourceConfiguration>> GetByNameAsync(string name, Ct ct)
    {
        var result = await repository.TryReadByNameAsync(name, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        var scopeNames = await GetScopeNamesAsync(dso.Id, ct);
        var schema = await schemaStore.GetAsync(SchemaId.ApiResource, ct);
        return GetResult.Found(MapToConfiguration(dso, scopeNames, schema), (DataVersion)(version));
    }

    public async Task<SaveResult<ApiResourceId>> UpdateAsync(ApiResourceId id, UpdateApiResource resource, DataVersion expectedVersion, Ct ct)
    {
        var structuralError = ValidateStructure(resource.Name, resource.UserClaims, resource.Scopes, resource.AllowedAccessTokenSigningAlgorithms);
        if (structuralError is not null)
        {
            return SaveResult.Failure<ApiResourceId>(structuralError);
        }

        var idValue = id.Value;
        var existing = await repository.TryReadByIdAsync(idValue, ct);
        if (existing is null)
        {
            return SaveResult.Failure<ApiResourceId>(StorageError.NotFound("api_resource", idValue.ToString()));
        }

        var (existingDso, _) = existing.Value;

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(resource.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<ApiResourceId>(extendedPropertiesError);
        }

        var (scopes, scopeError) = await ResolveScopesAsync(resource.Scopes, ct);
        if (scopeError is not null)
        {
            return SaveResult.Failure<ApiResourceId>(scopeError);
        }

        var dso = MapToDso(idValue, resource.Name, resource.Enabled, resource.DisplayName, resource.Description, resource.ShowInDiscoveryDocument, resource.RequireResourceIndicator, resource.UserClaims, resource.AllowedAccessTokenSigningAlgorithms, resource.ExtendedProperties, existingSecrets: existingDso.ApiSecrets);

        var currentScopes = await repository.GetScopesAsync(idValue, ct);
        var currentScopeIds = currentScopes.Select(s => s.Id).ToHashSet();
        var requestedScopeIds = scopes.Select(s => s.Id).ToHashSet();
        var scopesToLink = scopes.Where(s => !currentScopeIds.Contains(s.Id)).ToList();
        var scopesToUnlink = currentScopes.Where(s => !requestedScopeIds.Contains(s.Id)).Select(s => (s.Name, s.Id)).ToList();

        var outcome = await repository.UpdateAsync(
            Duende.Storage.UuidV7.From(idValue), dso, expectedVersion.Value, scopesToLink, scopesToUnlink, ct);

        return outcome.Kind switch
        {
            ApiResourceBatchOutcomeKind.Success => SaveResult.Success<ApiResourceId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
            ApiResourceBatchOutcomeKind.UnexpectedVersion => SaveResult.Failure<ApiResourceId>(StorageError.VersionConflict()),
            ApiResourceBatchOutcomeKind.ResourceDoesNotExist => SaveResult.Failure<ApiResourceId>(StorageError.NotFound("api_resource", idValue.ToString())),
            ApiResourceBatchOutcomeKind.KeyConflict => SaveResult.Failure<ApiResourceId>(StorageError.AlreadyExists("api_resource", resource.Name)),
            ApiResourceBatchOutcomeKind.ScopeDoesNotExist =>
                SaveResult.Failure<ApiResourceId>(StorageError.InvalidValue("Scopes", $"Scope '{outcome.ScopeName}' does not exist.")),
            _ => throw new InvalidOperationException($"Unexpected ApiResourceBatchOutcome: {outcome.Kind}")
        };
    }

    public async Task<SaveResult<ApiResourceId>> DeleteAsync(ApiResourceId id, Ct ct)
    {
        var idValue = id.Value;
        var existing = await repository.TryReadByIdAsync(idValue, ct);
        if (existing is null)
        {
            return SaveResult.Success<ApiResourceId>(idValue, (DataVersion)(0)); // idempotent: already gone
        }

        var result = await repository.DeleteAsync(idValue, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success<ApiResourceId>(idValue, (DataVersion)(0)),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<QueryResult<ApiResourceListItem>> QueryAsync(QueryRequest<ApiResourceFilter, ApiResourceSortField> request, Ct ct)
    {
        var result = await StorageMapping.ExecuteQueryAsync(() => repository.QueryAsync(request.ToStorage(), ct));

        var scopeCounts = await repository.CountScopesByResourceAsync(result.Items.Select(dso => dso.Id), ct);

        return result.ConvertTo(dso => MapToListItem(dso, scopeCounts[dso.Id])).ToModel(request.Range);
    }

    public async Task<SaveResult<ApiResourceSecretId>> CreateSecretAsync(
        ApiResourceId apiResourceId,
        string plaintextValue,
        SecretHashAlgorithm? hashAlgorithm,
        string? description,
        DateTime? expiration,
        string? type,
        Ct ct)
    {
        if (string.IsNullOrWhiteSpace(plaintextValue))
        {
            return SaveResult.Failure<ApiResourceSecretId>(StorageError.Required("plaintextValue"));
        }

        var apiResourceIdValue = apiResourceId.Value;
        var existing = await repository.TryReadByIdAsync(apiResourceIdValue, ct);
        if (existing is null)
        {
            return SaveResult.Failure<ApiResourceSecretId>(StorageError.NotFound("api_resource", apiResourceIdValue.ToString()));
        }

        var (dso, version) = existing.Value;

        var (value, algorithmName) = ShapeSecret(plaintextValue, hashAlgorithm, type);

        var secretId = Duende.Storage.UuidV7.New().Value;
        var newSecret = new ApiResourceDso.SecretDso(
            Id: secretId,
            Value: value,
            Description: description,
            Expiration: expiration,
            Type: type ?? IdentityServerConstants.SecretTypes.SharedSecret,
            HashAlgorithm: algorithmName);

        var updatedSecrets = dso.ApiSecrets.Append(newSecret).ToList();
        var updatedDso = dso with { ApiSecrets = updatedSecrets };

        var result = await repository.UpdateAsync(Duende.Storage.UuidV7.From(apiResourceIdValue), updatedDso, version, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success<ApiResourceSecretId>(secretId, (DataVersion)(version + 1)),
            UpdateResult.UnexpectedVersion => SaveResult.Failure<ApiResourceSecretId>(StorageError.VersionConflict()),
            UpdateResult.DoesNotExist => SaveResult.Failure<ApiResourceSecretId>(StorageError.NotFound("api_resource", apiResourceIdValue.ToString())),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<ApiResourceSecretId>> DeleteSecretAsync(ApiResourceId apiResourceId, ApiResourceSecretId secretId, Ct ct)
    {
        var apiResourceIdValue = apiResourceId.Value;
        var secretIdValue = secretId.Value;
        var existing = await repository.TryReadByIdAsync(apiResourceIdValue, ct);
        if (existing is null)
        {
            return SaveResult.Failure<ApiResourceSecretId>(StorageError.NotFound("api_resource", apiResourceIdValue.ToString()));
        }

        var (dso, version) = existing.Value;

        var secretToDelete = dso.ApiSecrets.FirstOrDefault(s => s.Id == secretIdValue);
        if (secretToDelete is null)
        {
            return SaveResult.Failure<ApiResourceSecretId>(StorageError.NotFound("secret", secretIdValue.ToString()));
        }

        var updatedSecrets = dso.ApiSecrets.Where(s => s.Id != secretIdValue).ToList();
        var updatedDso = dso with { ApiSecrets = updatedSecrets };

        var result = await repository.UpdateAsync(Duende.Storage.UuidV7.From(apiResourceIdValue), updatedDso, version, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success<ApiResourceSecretId>(secretIdValue, (DataVersion)(version + 1)),
            UpdateResult.UnexpectedVersion => SaveResult.Failure<ApiResourceSecretId>(StorageError.VersionConflict()),
            UpdateResult.DoesNotExist => SaveResult.Failure<ApiResourceSecretId>(StorageError.NotFound("api_resource", apiResourceIdValue.ToString())),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    private async Task<StorageError?> ValidateExtendedPropertiesAsync(AttributeValueCollection extendedProperties, Ct ct)
    {
        if (extendedProperties.Count == 0)
        {
            return null;
        }

        var schema = await schemaStore.GetAsync(SchemaId.ApiResource, ct);

        if (!extendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return StorageError.ValidationFailed(string.Join("; ", errors));
        }

        return null;
    }

    private static StorageError? ValidateStructure(string name, List<string>? userClaims, List<string>? scopes, List<string>? allowedAccessTokenSigningAlgorithms)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return StorageError.Required("Name");
        }

        if (userClaims is not null)
        {
            foreach (var claim in userClaims)
            {
                if (string.IsNullOrWhiteSpace(claim))
                {
                    return StorageError.InvalidValue("UserClaims", "Claim type must not be null or whitespace.");
                }
            }
        }

        if (scopes is not null)
        {
            foreach (var scope in scopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    return StorageError.InvalidValue("Scopes", "Scope name must not be null or whitespace.");
                }
            }

            if (scopes.Distinct(StringComparer.Ordinal).Count() != scopes.Count)
            {
                return StorageError.InvalidValue("Scopes", "Scope list contains duplicate names.");
            }
        }

        if (allowedAccessTokenSigningAlgorithms is not null)
        {
            foreach (var alg in allowedAccessTokenSigningAlgorithms)
            {
                if (string.IsNullOrWhiteSpace(alg))
                {
                    return StorageError.InvalidValue("AllowedAccessTokenSigningAlgorithms", "Algorithm must not be null or whitespace.");
                }
            }
        }

        return null;
    }

    private async Task<(List<(string Name, Guid Id)> Scopes, StorageError? Error)> ResolveScopesAsync(List<string>? scopeNames, Ct ct)
    {
        if (scopeNames is null || scopeNames.Count == 0)
        {
            return ([], null);
        }

        var found = await scopeRepository.FindByExactNamesAsync(scopeNames, ct);
        var scopes = new List<(string Name, Guid Id)>(scopeNames.Count);
        foreach (var name in scopeNames)
        {
            if (!found.TryGetValue(name, out var dso))
            {
                return ([], StorageError.InvalidValue("Scopes", $"Scope '{name}' does not exist."));
            }

            scopes.Add((name, dso.Id));
        }

        return (scopes, null);
    }

    private async Task<List<string>> GetScopeNamesAsync(Guid resourceId, Ct ct)
    {
        var scopes = await repository.GetScopesAsync(resourceId, ct);
        return scopes.Select(s => s.Name).Order(StringComparer.Ordinal).ToList();
    }

    private static ApiResourceDso.V1 MapToDso(
        Guid id,
        string name,
        bool enabled,
        string? displayName,
        string? description,
        bool showInDiscoveryDocument,
        bool requireResourceIndicator,
        List<string>? userClaims,
        List<string>? allowedAccessTokenSigningAlgorithms,
        AttributeValueCollection extendedProperties,
        IReadOnlyList<ApiResourceDso.SecretDso>? existingSecrets)
    {
        var secrets = existingSecrets ?? [];

        return new ApiResourceDso.V1
        {
            Id = id,
            Name = name,
            Enabled = enabled,
            DisplayName = displayName,
            Description = description,
            ShowInDiscoveryDocument = showInDiscoveryDocument,
            RequireResourceIndicator = requireResourceIndicator,
            UserClaims = userClaims?.AsReadOnly() ?? [],
            AllowedAccessTokenSigningAlgorithms = allowedAccessTokenSigningAlgorithms?.AsReadOnly() ?? [],
            ApiSecrets = secrets,
            ExtendedAttributeValues = EavMapper.ToDsoList(extendedProperties)
        };
    }

    private static ApiResourceConfiguration MapToConfiguration(ApiResourceDso.V1 dso, List<string> scopeNames, IReadOnlyAttributeSchema? schema) =>
        new()
        {
            Name = dso.Name,
            Enabled = dso.Enabled,
            DisplayName = dso.DisplayName,
            Description = dso.Description,
            ShowInDiscoveryDocument = dso.ShowInDiscoveryDocument,
            RequireResourceIndicator = dso.RequireResourceIndicator,
            UserClaims = new List<string>(dso.UserClaims),
            Scopes = scopeNames,
            AllowedAccessTokenSigningAlgorithms = new List<string>(dso.AllowedAccessTokenSigningAlgorithms),
            ExtendedProperties = EavMapper.ToAttributeValues(dso.ExtendedAttributeValues ?? [], schema).ToList(),
            ApiSecrets = dso.ApiSecrets
                .Select(s => new ApiResourceSecretConfiguration
                {
                    Id = s.Id,
                    Description = s.Description,
                    Expiration = s.Expiration,
                    Type = s.Type
                })
                .ToList()
        };

    private static ApiResourceListItem MapToListItem(ApiResourceDso.V1 dso, int scopeCount) =>
        new()
        {
            Id = dso.Id,
            Name = dso.Name,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Description = dso.Description,
            ScopeCount = scopeCount
        };

    private static string HashSecret(string plaintext, SecretHashAlgorithm algorithm)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return algorithm switch
        {
            SecretHashAlgorithm.Sha512 => Convert.ToBase64String(SHA512.HashData(bytes)),
            _ => Convert.ToBase64String(SHA256.HashData(bytes))
        };
    }

    private static (string Value, string HashAlgorithm) ShapeSecret(string plaintextValue, SecretHashAlgorithm? hashAlgorithm, string? type)
    {
        if (type is IdentityServerConstants.SecretTypes.X509CertificateThumbprint
            or IdentityServerConstants.SecretTypes.X509CertificateName
            or IdentityServerConstants.SecretTypes.X509CertificateBase64
            or IdentityServerConstants.SecretTypes.JsonWebKey)
        {
            return (plaintextValue, string.Empty);
        }

        var algorithm = hashAlgorithm ?? SecretHashAlgorithm.Sha256;
        return (HashSecret(plaintextValue, algorithm), algorithm == SecretHashAlgorithm.Sha512 ? "SHA512" : "SHA256");
    }
}
