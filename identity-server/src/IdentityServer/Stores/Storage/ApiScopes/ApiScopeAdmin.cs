// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Stores.Storage.ApiScopes;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiScopeAdmin(ApiScopeRepository repository, ISchemaStore schemaStore) : IApiScopeAdmin
{
    public async Task<SaveResult<Guid>> CreateAsync(ApiScopeConfiguration scope, Ct ct)
    {
        var structuralError = ValidateStructure(scope);
        if (structuralError is not null)
        {
            return structuralError;
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(scope, ct);
        if (extendedPropertiesError is not null)
        {
            return extendedPropertiesError;
        }

        var id = UuidV7.New();
        var dso = MapToDso(id.Value, scope);

        var result = await repository.CreateAsync(id, dso, ct);

        return result switch
        {
            CreateResult.Success => SaveResult.Success(id.Value, (DataVersion)1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                AdminError.AlreadyExists("api_scope", scope.Name),
            _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
        };
    }

    public async Task<GetResult<ApiScopeConfiguration>> GetAsync(Guid id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiScopeConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<GetResult<ApiScopeConfiguration>> GetByNameAsync(string name, Ct ct)
    {
        var result = await repository.TryReadByNameAsync(name, ct);
        if (result is null)
        {
            return GetResult.NotFound<ApiScopeConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<SaveResult<Guid>> UpdateAsync(Guid id, ApiScopeConfiguration scope, DataVersion expectedVersion, Ct ct)
    {
        var structuralError = ValidateStructure(scope);
        if (structuralError is not null)
        {
            return structuralError;
        }

        var existing = await repository.TryReadByIdAsync(id, ct);
        if (existing is null)
        {
            return AdminError.NotFound("api_scope", id.ToString());
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(scope, ct);
        if (extendedPropertiesError is not null)
        {
            return extendedPropertiesError;
        }

        var dso = MapToDso(id, scope);

        var result = await repository.UpdateAsync(UuidV7.From(id), dso, expectedVersion.Value, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success(id, (DataVersion)(expectedVersion.Value + 1)),
            UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
            UpdateResult.DoesNotExist => AdminError.NotFound("api_scope", id.ToString()),
            UpdateResult.KeyConflict => AdminError.AlreadyExists("api_scope", scope.Name),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct)
    {
        var result = await repository.DeleteAsync(id, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success(id, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<Duende.Storage.Querying.QueryResult<ApiScopeListItem>> QueryAsync(QueryRequest<ApiScopeFilter, ApiScopeSortField> request, Ct ct)
    {
        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    private async Task<AdminError?> ValidateExtendedPropertiesAsync(ApiScopeConfiguration scope, Ct ct)
    {
        if (scope.ExtendedProperties.Count == 0)
        {
            return null;
        }

        var schema = await schemaStore.GetAsync(SchemaId.ApiScope, ct);
        if (schema is null)
        {
            return AdminError.ValidationFailed(
                "ExtendedProperties cannot be used: no API scope schema is configured. " +
                "Register a schema via ISchemaStore to enable extended properties.");
        }

        if (!scope.ExtendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return AdminError.ValidationFailed(string.Join("; ", errors));
        }

        return null;
    }

    private static AdminError? ValidateStructure(ApiScopeConfiguration scope)
    {
        if (string.IsNullOrWhiteSpace(scope.Name))
        {
            return AdminError.Required("Name");
        }

        if (scope.UserClaims is not null)
        {
            foreach (var claim in scope.UserClaims)
            {
                if (string.IsNullOrWhiteSpace(claim))
                {
                    return AdminError.InvalidValue("UserClaims", "Claim type must not be null or whitespace.");
                }
            }
        }

        return null;
    }

    private static ApiScopeDso.V1 MapToDso(Guid id, ApiScopeConfiguration scope) =>
        new()
        {
            Id = id,
            Name = scope.Name,
            Enabled = scope.Enabled,
            DisplayName = scope.DisplayName,
            Description = scope.Description,
            ShowInDiscoveryDocument = scope.ShowInDiscoveryDocument,
            Required = scope.Required,
            Emphasize = scope.Emphasize,
            UserClaims = scope.UserClaims?.AsReadOnly() ?? [],
            ExtendedAttributeValues = EavPropertyMapper.SerializeFromCollection(scope.ExtendedProperties)
        };

    private static ApiScopeConfiguration MapToConfiguration(ApiScopeDso.V1 dso) =>
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
            ExtendedProperties = EavPropertyMapper.DeserializeToCollection(dso.ExtendedAttributeValues)
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
