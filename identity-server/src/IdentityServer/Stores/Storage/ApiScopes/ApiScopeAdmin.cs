// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Stores.Storage.ApiScopes;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiScopeAdmin(ApiScopeRepository repository, ISchemaStore schemaStore) : IApiScopeAdmin
{
    public async Task<SaveResult<ApiScopeId>> CreateAsync(CreateApiScope scope, Ct ct)
    {
        var structuralError = ValidateStructure(scope.Name, scope.UserClaims);
        if (structuralError is not null)
        {
            return SaveResult.Failure<ApiScopeId>(structuralError);
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(scope.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<ApiScopeId>(extendedPropertiesError);
        }

        var id = UuidV7.New();
        var dso = MapToDso(id.Value, scope.Name, scope.Enabled, scope.DisplayName, scope.Description, scope.ShowInDiscoveryDocument, scope.Required, scope.Emphasize, scope.UserClaims, scope.ExtendedProperties);

        var result = await repository.CreateAsync(id, dso, ct);

        return result switch
        {
            CreateResult.Success => SaveResult.Success<ApiScopeId>(id.Value, (DataVersion)1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("api_scope", scope.Name)),
            _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
        };
    }

    public async Task<GetResult<ApiScopeConfiguration>> GetAsync(ApiScopeId id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id.Value, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiScopeConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.ApiScope, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
    }

    public async Task<GetResult<ApiScopeConfiguration>> GetByNameAsync(string name, Ct ct)
    {
        var result = await repository.TryReadByNameAsync(name, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiScopeConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.ApiScope, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
    }

    public async Task<SaveResult<ApiScopeId>> UpdateAsync(ApiScopeId id, UpdateApiScope scope, DataVersion expectedVersion, Ct ct)
    {
        var structuralError = ValidateStructure(scope.Name, scope.UserClaims);
        if (structuralError is not null)
        {
            return SaveResult.Failure<ApiScopeId>(structuralError);
        }

        var idValue = id.Value;
        var existing = await repository.TryReadByIdAsync(idValue, ct);
        if (existing is null)
        {
            return SaveResult.Failure<ApiScopeId>(StorageError.NotFound("api_scope", idValue.ToString()));
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(scope.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<ApiScopeId>(extendedPropertiesError);
        }

        var dso = MapToDso(idValue, scope.Name, scope.Enabled, scope.DisplayName, scope.Description, scope.ShowInDiscoveryDocument, scope.Required, scope.Emphasize, scope.UserClaims, scope.ExtendedProperties);

        var result = await repository.UpdateAsync(UuidV7.From(idValue), dso, expectedVersion.Value, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success<ApiScopeId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
            UpdateResult.UnexpectedVersion => SaveResult.Failure<ApiScopeId>(StorageError.VersionConflict()),
            UpdateResult.DoesNotExist => SaveResult.Failure<ApiScopeId>(StorageError.NotFound("api_scope", idValue.ToString())),
            UpdateResult.KeyConflict => SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("api_scope", scope.Name)),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<ApiScopeId>> DeleteAsync(ApiScopeId id, Ct ct)
    {
        var idValue = id.Value;
        var result = await repository.DeleteAsync(idValue, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success<ApiScopeId>(idValue, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<QueryResult<ApiScopeListItem>> QueryAsync(QueryRequest<ApiScopeFilter, ApiScopeSortField> request, Ct ct)
    {
        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    private async Task<StorageError?> ValidateExtendedPropertiesAsync(AttributeValueCollection extendedProperties, Ct ct)
    {
        if (extendedProperties.Count == 0)
        {
            return null;
        }

        var schema = await schemaStore.GetAsync(SchemaId.ApiScope, ct);

        if (!extendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return StorageError.ValidationFailed(string.Join("; ", errors));
        }

        return null;
    }

    private static StorageError? ValidateStructure(string name, List<string>? userClaims)
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

        return null;
    }

    private static ApiScopeDso.V1 MapToDso(
        Guid id,
        string name,
        bool enabled,
        string? displayName,
        string? description,
        bool showInDiscoveryDocument,
        bool required,
        bool emphasize,
        List<string>? userClaims,
        AttributeValueCollection extendedProperties) =>
        new()
        {
            Id = id,
            Name = name,
            Enabled = enabled,
            DisplayName = displayName,
            Description = description,
            ShowInDiscoveryDocument = showInDiscoveryDocument,
            Required = required,
            Emphasize = emphasize,
            UserClaims = userClaims?.AsReadOnly() ?? [],
            ExtendedAttributeValues = EavMapper.ToDsoList(extendedProperties)
        };

    private static ApiScopeConfiguration MapToConfiguration(ApiScopeDso.V1 dso, IReadOnlyAttributeSchema? schema) =>
        new()
        {
            Name = dso.Name,
            Enabled = dso.Enabled,
            DisplayName = dso.DisplayName,
            Description = dso.Description,
            ShowInDiscoveryDocument = dso.ShowInDiscoveryDocument,
            Required = dso.Required,
            Emphasize = dso.Emphasize,
            UserClaims = new List<string>(dso.UserClaims),
            ExtendedProperties = EavMapper.ToAttributeValues(dso.ExtendedAttributeValues ?? [], schema).ToList()
        };

    private static ApiScopeListItem MapToListItem(ApiScopeDso.V1 dso) =>
        new()
        {
            Id = dso.Id,
            Name = dso.Name,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Description = dso.Description
        };
}
