// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
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

        var scopeRefsResult = await ResolveScopeRefsAsync(resource.Scopes, ct);
        if (scopeRefsResult.Error is not null)
        {
            return SaveResult.Failure<ApiResourceId>(scopeRefsResult.Error);
        }

        var scopeRefs = scopeRefsResult.ScopeRefs;
        var id = UuidV7.New();
        var dso = MapToDso(id.Value, resource.Name, resource.Enabled, resource.DisplayName, resource.Description, resource.ShowInDiscoveryDocument, resource.RequireResourceIndicator, resource.UserClaims, resource.AllowedAccessTokenSigningAlgorithms, resource.ExtendedProperties, scopeRefs, existingSecrets: null);

        if (scopeRefs.Count > 0)
        {
            var result = await repository.CreateWithScopesAsync(id, dso, scopeRefs, ct);
            return result switch
            {
                CreateResult.Success => SaveResult.Success<ApiResourceId>(id.Value, (DataVersion)1),
                CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                    SaveResult.Failure<ApiResourceId>(StorageError.AlreadyExists("api_resource", resource.Name)),
                _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
            };
        }
        else
        {
            var result = await repository.CreateAsync(id, dso, ct);
            return result switch
            {
                CreateResult.Success => SaveResult.Success<ApiResourceId>(id.Value, (DataVersion)1),
                CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                    SaveResult.Failure<ApiResourceId>(StorageError.AlreadyExists("api_resource", resource.Name)),
                _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
            };
        }
    }

    public async Task<GetResult<ApiResourceConfiguration>> GetAsync(ApiResourceId id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id.Value, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.ApiResource, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
    }

    public async Task<GetResult<ApiResourceConfiguration>> GetByNameAsync(string name, Ct ct)
    {
        var result = await repository.TryReadByNameAsync(name, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.ApiResource, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
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

        var scopeRefsResult = await ResolveScopeRefsAsync(resource.Scopes, ct);
        if (scopeRefsResult.Error is not null)
        {
            return SaveResult.Failure<ApiResourceId>(scopeRefsResult.Error);
        }

        var newScopeRefs = scopeRefsResult.ScopeRefs;
        var dso = MapToDso(idValue, resource.Name, resource.Enabled, resource.DisplayName, resource.Description, resource.ShowInDiscoveryDocument, resource.RequireResourceIndicator, resource.UserClaims, resource.AllowedAccessTokenSigningAlgorithms, resource.ExtendedProperties, newScopeRefs, existingSecrets: existingDso.ApiSecrets);

        // Diff scopes. On rename, treat all existing scopes as removed and all new scopes as added
        // so that back-references on ApiScope are updated with the new resource name.
        List<ApiScopeReferenceDso.V1> addedScopeRefs;
        List<Guid> removedScopeIds;
        var isRename = resource.Name != existingDso.Name;

        if (isRename)
        {
            addedScopeRefs = new List<ApiScopeReferenceDso.V1>(newScopeRefs);
            removedScopeIds = existingDso.Scopes.Select(s => s.Id).ToList();
        }
        else
        {
            var existingScopeIdSet = existingDso.Scopes.ToDictionary(s => s.Id);
            var newScopeIdSet = newScopeRefs.ToDictionary(s => s.Id);

            addedScopeRefs = newScopeRefs.Where(s => !existingScopeIdSet.ContainsKey(s.Id)).ToList();
            removedScopeIds = existingDso.Scopes
                .Where(s => !newScopeIdSet.ContainsKey(s.Id))
                .Select(s => s.Id)
                .ToList();
        }

        if (addedScopeRefs.Count > 0 || removedScopeIds.Count > 0)
        {
            var result = await repository.UpdateWithScopeChangesAsync(
                UuidV7.From(idValue), dso, expectedVersion.Value, addedScopeRefs, removedScopeIds, ct);

            return result switch
            {
                UpdateResult.Success => SaveResult.Success<ApiResourceId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
                UpdateResult.UnexpectedVersion => SaveResult.Failure<ApiResourceId>(StorageError.VersionConflict()),
                UpdateResult.DoesNotExist => SaveResult.Failure<ApiResourceId>(StorageError.NotFound("api_resource", idValue.ToString())),
                UpdateResult.KeyConflict => SaveResult.Failure<ApiResourceId>(StorageError.AlreadyExists("api_resource", resource.Name)),
                _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
            };
        }
        else
        {
            var result = await repository.UpdateAsync(UuidV7.From(idValue), dso, expectedVersion.Value, ct);

            return result switch
            {
                UpdateResult.Success => SaveResult.Success<ApiResourceId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
                UpdateResult.UnexpectedVersion => SaveResult.Failure<ApiResourceId>(StorageError.VersionConflict()),
                UpdateResult.DoesNotExist => SaveResult.Failure<ApiResourceId>(StorageError.NotFound("api_resource", idValue.ToString())),
                UpdateResult.KeyConflict => SaveResult.Failure<ApiResourceId>(StorageError.AlreadyExists("api_resource", resource.Name)),
                _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
            };
        }
    }

    public async Task<SaveResult<ApiResourceId>> DeleteAsync(ApiResourceId id, Ct ct)
    {
        var idValue = id.Value;
        var existing = await repository.TryReadByIdAsync(idValue, ct);
        if (existing is null)
        {
            return SaveResult.Success<ApiResourceId>(idValue, (DataVersion)0); // idempotent: already gone
        }

        var (dso, _) = existing.Value;

        DeleteResult result;
        if (dso.Scopes.Count > 0)
        {
            result = await repository.DeleteWithScopeCleanupAsync(idValue, dso.Scopes, ct);
        }
        else
        {
            result = await repository.DeleteAsync(idValue, ct);
        }

        return result switch
        {
            DeleteResult.Success => SaveResult.Success<ApiResourceId>(idValue, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<QueryResult<ApiResourceListItem>> QueryAsync(QueryRequest<ApiResourceFilter, ApiResourceSortField> request, Ct ct)
    {
        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
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

        var algorithm = hashAlgorithm ?? SecretHashAlgorithm.Sha256;
        var hashedValue = HashSecret(plaintextValue, algorithm);
        var algorithmName = algorithm == SecretHashAlgorithm.Sha512 ? "SHA512" : "SHA256";

        var secretId = UuidV7.New().Value;
        var newSecret = new ApiResourceDso.SecretDso(
            Id: secretId,
            Value: hashedValue,
            Description: description,
            Expiration: expiration,
            Type: type ?? IdentityServerConstants.SecretTypes.SharedSecret,
            HashAlgorithm: algorithmName);

        var updatedSecrets = dso.ApiSecrets.Append(newSecret).ToList();
        var updatedDso = dso with { ApiSecrets = updatedSecrets };

        var result = await repository.UpdateAsync(UuidV7.From(apiResourceIdValue), updatedDso, version, ct);

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

        var result = await repository.UpdateAsync(UuidV7.From(apiResourceIdValue), updatedDso, version, ct);

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

    private sealed record ScopeRefsResult(IReadOnlyList<ApiScopeReferenceDso.V1> ScopeRefs, StorageError? Error)
    {
        internal static ScopeRefsResult Success(IReadOnlyList<ApiScopeReferenceDso.V1> refs) => new(refs, null);
        internal static ScopeRefsResult Failure(StorageError error) => new([], error);
    }

    private async Task<ScopeRefsResult> ResolveScopeRefsAsync(List<string>? scopeNames, Ct ct)
    {
        if (scopeNames is null || scopeNames.Count == 0)
        {
            return ScopeRefsResult.Success([]);
        }

        var distinctNames = scopeNames.Distinct(StringComparer.Ordinal).ToList();
        var foundScopes = await scopeRepository.FindByNamesAsync(distinctNames, ct);
        var foundByName = foundScopes.ToDictionary(s => s.Name, StringComparer.Ordinal);

        // Validate all requested scopes exist
        foreach (var name in distinctNames)
        {
            if (!foundByName.ContainsKey(name))
            {
                return ScopeRefsResult.Failure(
                    StorageError.InvalidValue("Scopes", $"Scope '{name}' does not exist."));
            }
        }

        var refs = distinctNames
            .Select(name => new ApiScopeReferenceDso.V1(foundByName[name].Id, name))
            .ToList();

        return ScopeRefsResult.Success(refs);
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
        IReadOnlyList<ApiScopeReferenceDso.V1> scopeRefs,
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
            Scopes = scopeRefs,
            AllowedAccessTokenSigningAlgorithms = allowedAccessTokenSigningAlgorithms?.AsReadOnly() ?? [],
            ApiSecrets = secrets,
            ExtendedAttributeValues = EavMapper.ToDsoList(extendedProperties)
        };
    }

    private static ApiResourceConfiguration MapToConfiguration(ApiResourceDso.V1 dso, IReadOnlyAttributeSchema? schema) =>
        new()
        {
            Name = dso.Name,
            Enabled = dso.Enabled,
            DisplayName = dso.DisplayName,
            Description = dso.Description,
            ShowInDiscoveryDocument = dso.ShowInDiscoveryDocument,
            RequireResourceIndicator = dso.RequireResourceIndicator,
            UserClaims = new List<string>(dso.UserClaims),
            Scopes = dso.Scopes.Select(s => s.Name).ToList(),
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

    private static ApiResourceListItem MapToListItem(ApiResourceDso.V1 dso) =>
        new()
        {
            Id = dso.Id,
            Name = dso.Name,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Description = dso.Description,
            ScopeCount = dso.Scopes.Count
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
}
