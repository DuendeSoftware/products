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
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using SecretHashAlgorithm = Duende.IdentityServer.Admin.SecretHashAlgorithm;

namespace Duende.IdentityServer.Stores.Storage.ApiResources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiResourceAdmin(ApiResourceRepository repository, ApiScopeRepository scopeRepository, ISchemaStore schemaStore) : IApiResourceAdmin
{
    public async Task<SaveResult<Guid>> CreateAsync(ApiResourceConfiguration resource, Ct ct)
    {
        var structuralError = ValidateStructure(resource);
        if (structuralError is not null)
        {
            return structuralError;
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(resource, ct);
        if (extendedPropertiesError is not null)
        {
            return extendedPropertiesError;
        }

        var scopeRefsResult = await ResolveScopeRefsAsync(resource.Scopes, ct);
        if (scopeRefsResult.Error is not null)
        {
            return scopeRefsResult.Error;
        }

        var scopeRefs = scopeRefsResult.ScopeRefs;
        var id = UuidV7.New();
        var dso = MapToDso(id.Value, resource, scopeRefs, existingSecrets: null);

        if (scopeRefs.Count > 0)
        {
            var result = await repository.CreateWithScopesAsync(id, dso, scopeRefs, ct);
            return result switch
            {
                CreateResult.Success => SaveResult.Success(id.Value, (DataVersion)1),
                CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                    AdminError.AlreadyExists("api_resource", resource.Name),
                _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
            };
        }
        else
        {
            var result = await repository.CreateAsync(id, dso, ct);
            return result switch
            {
                CreateResult.Success => SaveResult.Success(id.Value, (DataVersion)1),
                CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                    AdminError.AlreadyExists("api_resource", resource.Name),
                _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
            };
        }
    }

    public async Task<GetResult<ApiResourceConfiguration>> GetAsync(Guid id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<GetResult<ApiResourceConfiguration>> GetByNameAsync(string name, Ct ct)
    {
        var result = await repository.TryReadByNameAsync(name, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<SaveResult<Guid>> UpdateAsync(Guid id, ApiResourceConfiguration resource, DataVersion expectedVersion, Ct ct)
    {
        var structuralError = ValidateStructure(resource);
        if (structuralError is not null)
        {
            return structuralError;
        }

        var existing = await repository.TryReadByIdAsync(id, ct);
        if (existing is null)
        {
            return AdminError.NotFound("api_resource", id.ToString());
        }

        var (existingDso, _) = existing.Value;

        // Validate: all ApiResourceSecretConfiguration IDs must exist in the current DSO
        if (resource.ApiSecrets is not null)
        {
            if (resource.ApiSecrets.Select(s => s.Id).Distinct().Count() != resource.ApiSecrets.Count)
            {
                return AdminError.InvalidValue("ApiSecrets", "Secret list contains duplicate IDs.");
            }

            var existingSecretIds = existingDso.ApiSecrets.Select(s => s.Id).ToHashSet();
            foreach (var secret in resource.ApiSecrets)
            {
                if (!existingSecretIds.Contains(secret.Id))
                {
                    return AdminError.InvalidValue("ApiSecrets", $"Secret with id '{secret.Id}' does not exist.");
                }
            }
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(resource, ct);
        if (extendedPropertiesError is not null)
        {
            return extendedPropertiesError;
        }

        var scopeRefsResult = await ResolveScopeRefsAsync(resource.Scopes, ct);
        if (scopeRefsResult.Error is not null)
        {
            return scopeRefsResult.Error;
        }

        var newScopeRefs = scopeRefsResult.ScopeRefs;
        var dso = MapToDso(id, resource, newScopeRefs, existingSecrets: existingDso.ApiSecrets);

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
                UuidV7.From(id), dso, expectedVersion.Value, addedScopeRefs, removedScopeIds, ct);

            return result switch
            {
                UpdateResult.Success => SaveResult.Success(id, (DataVersion)(expectedVersion.Value + 1)),
                UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
                UpdateResult.DoesNotExist => AdminError.NotFound("api_resource", id.ToString()),
                UpdateResult.KeyConflict => AdminError.AlreadyExists("api_resource", resource.Name),
                _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
            };
        }
        else
        {
            var result = await repository.UpdateAsync(UuidV7.From(id), dso, expectedVersion.Value, ct);

            return result switch
            {
                UpdateResult.Success => SaveResult.Success(id, (DataVersion)(expectedVersion.Value + 1)),
                UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
                UpdateResult.DoesNotExist => AdminError.NotFound("api_resource", id.ToString()),
                UpdateResult.KeyConflict => AdminError.AlreadyExists("api_resource", resource.Name),
                _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
            };
        }
    }

    public async Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct)
    {
        var existing = await repository.TryReadByIdAsync(id, ct);
        if (existing is null)
        {
            return SaveResult.Success(id, (DataVersion)0); // idempotent — already gone
        }

        var (dso, _) = existing.Value;

        DeleteResult result;
        if (dso.Scopes.Count > 0)
        {
            result = await repository.DeleteWithScopeCleanupAsync(id, dso.Scopes, ct);
        }
        else
        {
            result = await repository.DeleteAsync(id, ct);
        }

        return result switch
        {
            DeleteResult.Success => SaveResult.Success(id, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<Duende.Storage.Querying.QueryResult<ApiResourceListItem>> QueryAsync(QueryRequest<ApiResourceFilter, ApiResourceSortField> request, Ct ct)
    {
        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    public async Task<SaveResult<Guid>> CreateSecretAsync(
        Guid apiResourceId,
        string plaintextValue,
        SecretHashAlgorithm? hashAlgorithm,
        string? description,
        DateTime? expiration,
        string? type,
        Ct ct)
    {
        if (string.IsNullOrWhiteSpace(plaintextValue))
        {
            return AdminError.Required("plaintextValue");
        }

        var existing = await repository.TryReadByIdAsync(apiResourceId, ct);
        if (existing is null)
        {
            return AdminError.NotFound("api_resource", apiResourceId.ToString());
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

        var result = await repository.UpdateAsync(UuidV7.From(apiResourceId), updatedDso, version, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success(secretId, (DataVersion)(version + 1)),
            UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
            UpdateResult.DoesNotExist => AdminError.NotFound("api_resource", apiResourceId.ToString()),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<Guid>> DeleteSecretAsync(Guid apiResourceId, Guid secretId, Ct ct)
    {
        var existing = await repository.TryReadByIdAsync(apiResourceId, ct);
        if (existing is null)
        {
            return AdminError.NotFound("api_resource", apiResourceId.ToString());
        }

        var (dso, version) = existing.Value;

        var secretToDelete = dso.ApiSecrets.FirstOrDefault(s => s.Id == secretId);
        if (secretToDelete is null)
        {
            return AdminError.NotFound("secret", secretId.ToString());
        }

        var updatedSecrets = dso.ApiSecrets.Where(s => s.Id != secretId).ToList();
        var updatedDso = dso with { ApiSecrets = updatedSecrets };

        var result = await repository.UpdateAsync(UuidV7.From(apiResourceId), updatedDso, version, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success(secretId, (DataVersion)(version + 1)),
            UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
            UpdateResult.DoesNotExist => AdminError.NotFound("api_resource", apiResourceId.ToString()),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    private async Task<AdminError?> ValidateExtendedPropertiesAsync(ApiResourceConfiguration resource, Ct ct)
    {
        if (resource.ExtendedProperties.Count == 0)
        {
            return null;
        }

        var schema = await schemaStore.GetAsync(SchemaId.ApiResource, ct);
        if (schema is null)
        {
            return AdminError.ValidationFailed(
                "ExtendedProperties cannot be used: no API resource schema is configured. " +
                "Register a schema via ISchemaStore to enable extended properties.");
        }

        if (!resource.ExtendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return AdminError.ValidationFailed(string.Join("; ", errors));
        }

        return null;
    }

    private static AdminError? ValidateStructure(ApiResourceConfiguration resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Name))
        {
            return AdminError.Required("Name");
        }

        if (resource.UserClaims is not null)
        {
            foreach (var claim in resource.UserClaims)
            {
                if (string.IsNullOrWhiteSpace(claim))
                {
                    return AdminError.InvalidValue("UserClaims", "Claim type must not be null or whitespace.");
                }
            }
        }

        if (resource.Scopes is not null)
        {
            foreach (var scope in resource.Scopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    return AdminError.InvalidValue("Scopes", "Scope name must not be null or whitespace.");
                }
            }

            if (resource.Scopes.Distinct(StringComparer.Ordinal).Count() != resource.Scopes.Count)
            {
                return AdminError.InvalidValue("Scopes", "Scope list contains duplicate names.");
            }
        }

        if (resource.AllowedAccessTokenSigningAlgorithms is not null)
        {
            foreach (var alg in resource.AllowedAccessTokenSigningAlgorithms)
            {
                if (string.IsNullOrWhiteSpace(alg))
                {
                    return AdminError.InvalidValue("AllowedAccessTokenSigningAlgorithms", "Algorithm must not be null or whitespace.");
                }
            }
        }

        return null;
    }

    private sealed record ScopeRefsResult(IReadOnlyList<ApiScopeReferenceDso.V1> ScopeRefs, AdminError? Error)
    {
        internal static ScopeRefsResult Success(IReadOnlyList<ApiScopeReferenceDso.V1> refs) => new(refs, null);
        internal static ScopeRefsResult Failure(AdminError error) => new([], error);
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
                    AdminError.InvalidValue("Scopes", $"Scope '{name}' does not exist."));
            }
        }

        var refs = distinctNames
            .Select(name => new ApiScopeReferenceDso.V1(foundByName[name].Id, name))
            .ToList();

        return ScopeRefsResult.Success(refs);
    }

    private static ApiResourceDso.V1 MapToDso(Guid id, ApiResourceConfiguration resource, IReadOnlyList<ApiScopeReferenceDso.V1> scopeRefs, IReadOnlyList<ApiResourceDso.SecretDso>? existingSecrets)
    {
        var secrets = BuildSecrets(resource.ApiSecrets, existingSecrets);

        return new ApiResourceDso.V1
        {
            Id = id,
            Name = resource.Name,
            Enabled = resource.Enabled,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
            RequireResourceIndicator = resource.RequireResourceIndicator,
            UserClaims = resource.UserClaims?.AsReadOnly() ?? [],
            Scopes = scopeRefs,
            AllowedAccessTokenSigningAlgorithms = resource.AllowedAccessTokenSigningAlgorithms?.AsReadOnly() ?? [],
            ApiSecrets = secrets,
            ExtendedAttributeValues = EavPropertyMapper.SerializeFromCollection(resource.ExtendedProperties)
        };
    }

    private static IReadOnlyList<ApiResourceDso.SecretDso> BuildSecrets(
        List<ApiResourceSecretConfiguration>? configSecrets,
        IReadOnlyList<ApiResourceDso.SecretDso>? existingSecrets)
    {
        if (existingSecrets is null)
        {
            return [];
        }

        if (configSecrets is null || configSecrets.Count == 0)
        {
            return existingSecrets;
        }

        var metadataById = configSecrets.ToDictionary(s => s.Id);

        return existingSecrets
            .Select(existing =>
            {
                if (!metadataById.TryGetValue(existing.Id, out var updated))
                {
                    return existing;
                }

                return existing with
                {
                    Description = updated.Description,
                    Expiration = updated.Expiration,
                    Type = updated.Type
                };
            })
            .ToList();
    }

    private static ApiResourceConfiguration MapToConfiguration(ApiResourceDso.V1 dso) =>
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
            ExtendedProperties = EavPropertyMapper.DeserializeToCollection(dso.ExtendedAttributeValues),
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
