// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.ApiResources;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.IdentityServer.Stores.Storage.IdentityResources;
using Duende.Storage.Internal.Operations;

namespace Duende.IdentityServer.Stores.Storage.ApiScopes;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiScopeAdmin(
    ApiScopeRepository repository,
    IdentityResourceRepository identityResourceRepository,
    ApiResourceRepository resourceRepository,
    ClientRepository clientRepository,
    ISchemaStore schemaStore) : IApiScopeAdmin
{
    private const int MaxResourcesInError = 20;

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

        var id = Duende.Storage.UuidV7.New();
        var dso = MapToDso(id.Value, scope.Name, scope.Enabled, scope.DisplayName, scope.Description, scope.ShowInDiscoveryDocument, scope.Required, scope.Emphasize, scope.UserClaims, scope.ExtendedProperties);

        // The ScopeNameDso guard catches concurrent creates of the same name, but a row saved before the
        // guard existed has no guard to catch against. This read catches that legacy case.
        if (await identityResourceRepository.TryReadByNameAsync(scope.Name, ct) is not null)
        {
            return SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("identity_resource", scope.Name));
        }

        var result = await repository.CreateAsync(id, dso, ct);

        return result.Kind switch
        {
            ScopeNameBatchOutcomeKind.Success => SaveResult.Success<ApiScopeId>(id.Value, (DataVersion)(1)),
            ScopeNameBatchOutcomeKind.AlreadyExists =>
                SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("api_scope", scope.Name)),
            ScopeNameBatchOutcomeKind.NameTakenByOtherKind =>
                SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("identity_resource", scope.Name)),
            _ => throw new InvalidOperationException($"Unexpected ScopeNameBatchOutcomeKind: {result.Kind}")
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
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)(version));
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
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)(version));
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

        var previousName = existing.Value.Dso.Name;
        if (!string.Equals(previousName, scope.Name, StringComparison.Ordinal)
            && await identityResourceRepository.TryReadByNameAsync(scope.Name, ct) is not null)
        {
            return SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("identity_resource", scope.Name));
        }

        var result = await repository.UpdateAsync(Duende.Storage.UuidV7.From(idValue), dso, expectedVersion.Value, existing.Value.Dso.Name, ct);

        return result.Kind switch
        {
            ScopeNameBatchOutcomeKind.Success => SaveResult.Success<ApiScopeId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
            ScopeNameBatchOutcomeKind.UnexpectedVersion => SaveResult.Failure<ApiScopeId>(StorageError.VersionConflict()),
            ScopeNameBatchOutcomeKind.DoesNotExist => SaveResult.Failure<ApiScopeId>(StorageError.NotFound("api_scope", idValue.ToString())),
            ScopeNameBatchOutcomeKind.AlreadyExists => SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("api_scope", scope.Name)),
            ScopeNameBatchOutcomeKind.NameTakenByOtherKind =>
                SaveResult.Failure<ApiScopeId>(StorageError.AlreadyExists("identity_resource", scope.Name)),
            _ => throw new InvalidOperationException($"Unexpected ScopeNameBatchOutcomeKind: {result.Kind}")
        };
    }

    public async Task<SaveResult<ApiScopeId>> DeleteAsync(ApiScopeId id, Ct ct)
    {
        var idValue = id.Value;
        var result = await repository.DeleteAsync(idValue, ct);

        return result switch
        {
            // Deleting an already-missing scope is idempotent: Storage reports Success either way.
            DeleteResult.Success => SaveResult.Success<ApiScopeId>(idValue, (DataVersion)(0)),
            DeleteResult.StillLinked => SaveResult.Failure<ApiScopeId>(await BuildInUseErrorAsync(idValue, ct)),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<QueryResult<ApiScopeListItem>> QueryAsync(QueryRequest<ApiScopeFilter, ApiScopeSortField> request, Ct ct)
    {
        var result = await StorageMapping.ExecuteQueryAsync(() => repository.QueryAsync(request.ToStorage(), ct));
        return result.ConvertTo(MapToListItem).ToModel(request.Range);
    }

    private async Task<StorageError> BuildInUseErrorAsync(Guid scopeId, Ct ct)
    {
        var fragments = new List<string>();

        var resources = await resourceRepository.QueryByScopeIdAsync(scopeId, StorageDataRange.FromPage(1, MaxResourcesInError), ct);
        AppendUsageFragment(fragments, "API resources", resources, r => r.Name);

        var clients = await clientRepository.QueryByAllowedApiScopeIdAsync(scopeId, StorageDataRange.FromPage(1, MaxResourcesInError), ct);
        AppendUsageFragment(fragments, "clients", clients, c => c.ClientId);

        if (fragments.Count == 0)
        {
            return StorageError.ValidationFailed(
                "The API scope is still in use. Remove its references before deleting it.");
        }

        return StorageError.ValidationFailed(
            $"The API scope is used by {string.Join(" and ", fragments)}. Remove it from them before deleting it.");
    }

    /// <summary>
    /// Appends a "&lt;label&gt; 'name1', 'name2' and N more" fragment to <paramref name="fragments"/> for a
    /// page of entities that are using the scope being deleted, if the page is non-empty.
    /// </summary>
    private static void AppendUsageFragment<T>(List<string> fragments, string label, Duende.Storage.Internal.Querying.QueryResult<T> page, Func<T, string> nameSelector)
    {
        if (page.Items.Count == 0)
        {
            return;
        }

        var names = string.Join(", ", page.Items.Select(i => $"'{nameSelector(i)}'"));
        var remaining = (page.TotalCount ?? page.Items.Count) - page.Items.Count;
        if (remaining > 0)
        {
            names += $" and {remaining} more";
        }

        fragments.Add($"{label} {names}");
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
