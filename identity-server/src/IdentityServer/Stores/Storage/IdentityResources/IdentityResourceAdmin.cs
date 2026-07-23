// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Stores.Storage.IdentityResources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class IdentityResourceAdmin(IdentityResourceRepository repository, ISchemaStore schemaStore) : IIdentityResourceAdmin
{
    public async Task<SaveResult<Guid>> CreateAsync(IdentityResourceConfiguration resource, Ct ct)
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

        var id = UuidV7.New();
        var dso = MapToDso(id.Value, resource);

        var result = await repository.CreateAsync(id, dso, ct);

        return result switch
        {
            CreateResult.Success => SaveResult.Success(id.Value, (DataVersion)1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                AdminError.AlreadyExists("identity_resource", resource.Name),
            _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
        };
    }

    public async Task<GetResult<IdentityResourceConfiguration>> GetAsync(Guid id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id, ct);
        if (result is null)
        {
            return GetResult.NotFound<IdentityResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<GetResult<IdentityResourceConfiguration>> GetByNameAsync(string name, Ct ct)
    {
        var result = await repository.TryReadByNameAsync(name, ct);
        if (result is null)
        {
            return GetResult.NotFound<IdentityResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<SaveResult<Guid>> UpdateAsync(Guid id, IdentityResourceConfiguration resource, DataVersion expectedVersion, Ct ct)
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

        var existing = await repository.TryReadByIdAsync(id, ct);
        if (existing is null)
        {
            return AdminError.NotFound("identity_resource", id.ToString());
        }

        var dso = MapToDso(id, resource);

        var result = await repository.UpdateAsync(UuidV7.From(id), dso, expectedVersion.Value, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success(id, (DataVersion)(expectedVersion.Value + 1)),
            UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
            UpdateResult.DoesNotExist => AdminError.NotFound("identity_resource", id.ToString()),
            UpdateResult.KeyConflict => AdminError.AlreadyExists("identity_resource", resource.Name),
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

    public async Task<Duende.Storage.Querying.QueryResult<IdentityResourceListItem>> QueryAsync(QueryRequest<IdentityResourceFilter, IdentityResourceSortField> request, Ct ct)
    {
        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    private async Task<AdminError?> ValidateExtendedPropertiesAsync(IdentityResourceConfiguration resource, Ct ct)
    {
        if (resource.ExtendedProperties.Count == 0)
        {
            return null;
        }

        var schema = await schemaStore.GetAsync(SchemaId.IdentityResource, ct);
        if (schema is null)
        {
            return AdminError.ValidationFailed(
                "ExtendedProperties cannot be used: no identity resource schema is configured. " +
                "Register a schema via ISchemaStore to enable extended properties.");
        }

        if (!resource.ExtendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return AdminError.ValidationFailed(string.Join("; ", errors));
        }

        return null;
    }

    private static AdminError? ValidateStructure(IdentityResourceConfiguration resource)
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

        return null;
    }

    private static IdentityResourceDso.V1 MapToDso(Guid id, IdentityResourceConfiguration resource) =>
        new()
        {
            Id = id,
            Name = resource.Name,
            Enabled = resource.Enabled,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
            Required = resource.Required,
            Emphasize = resource.Emphasize,
            UserClaims = resource.UserClaims?.AsReadOnly() ?? [],
            ExtendedAttributeValues = EavPropertyMapper.SerializeFromCollection(resource.ExtendedProperties)
        };

    private static IdentityResourceConfiguration MapToConfiguration(IdentityResourceDso.V1 dso) =>
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

    private static IdentityResourceListItem MapToListItem(IdentityResourceDso.V1 dso) =>
        new()
        {
            Id = dso.Id,
            Name = dso.Name,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Description = dso.Description
        };
}
