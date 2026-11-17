// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.IdentityServer.Stores.Storage.SamlServiceProviders;
using Duende.Storage.Internal.Operations;

namespace Duende.IdentityServer.Stores.Storage.IdentityResources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class IdentityResourceAdmin(
    IdentityResourceRepository repository,
    ApiScopeRepository apiScopeRepository,
    ClientRepository clientRepository,
    SamlServiceProviderRepository samlServiceProviderRepository,
    ISchemaStore schemaStore) : IIdentityResourceAdmin
{
    private const int MaxResourcesInError = 20;
    public async Task<SaveResult<IdentityResourceId>> CreateAsync(CreateIdentityResource resource, Ct ct)
    {
        var structuralError = ValidateStructure(resource.Name, resource.UserClaims);
        if (structuralError is not null)
        {
            return SaveResult.Failure<IdentityResourceId>(structuralError);
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(resource.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<IdentityResourceId>(extendedPropertiesError);
        }

        var id = Duende.Storage.UuidV7.New();
        var dso = MapToDso(id.Value, resource.Name, resource.Enabled, resource.DisplayName, resource.Description, resource.ShowInDiscoveryDocument, resource.Required, resource.Emphasize, resource.UserClaims, resource.ExtendedProperties);

        // The ScopeNameDso guard catches concurrent creates of the same name, but a row saved before the
        // guard existed has no guard to catch against. This read catches that legacy case.
        if (await apiScopeRepository.TryReadByNameAsync(resource.Name, ct) is not null)
        {
            return SaveResult.Failure<IdentityResourceId>(StorageError.AlreadyExists("api_scope", resource.Name));
        }

        var result = await repository.CreateAsync(id, dso, ct);

        return result.Kind switch
        {
            ScopeNameBatchOutcomeKind.Success => SaveResult.Success<IdentityResourceId>(id.Value, (DataVersion)(1)),
            ScopeNameBatchOutcomeKind.AlreadyExists =>
                SaveResult.Failure<IdentityResourceId>(StorageError.AlreadyExists("identity_resource", resource.Name)),
            ScopeNameBatchOutcomeKind.NameTakenByOtherKind =>
                SaveResult.Failure<IdentityResourceId>(StorageError.AlreadyExists("api_scope", resource.Name)),
            _ => throw new InvalidOperationException($"Unexpected ScopeNameBatchOutcomeKind: {result.Kind}")
        };
    }

    public async Task<GetResult<IdentityResourceConfiguration>> GetAsync(IdentityResourceId id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id.Value, ct);
        if (result is null)
        {
            return GetResult.NotFound<IdentityResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.IdentityResource, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)(version));
    }

    public async Task<GetResult<IdentityResourceConfiguration>> GetByNameAsync(string name, Ct ct)
    {
        var result = await repository.TryReadByNameAsync(name, ct);
        if (result is null)
        {
            return GetResult.NotFound<IdentityResourceConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.IdentityResource, ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)(version));
    }

    public async Task<SaveResult<IdentityResourceId>> UpdateAsync(IdentityResourceId id, UpdateIdentityResource resource, DataVersion expectedVersion, Ct ct)
    {
        var structuralError = ValidateStructure(resource.Name, resource.UserClaims);
        if (structuralError is not null)
        {
            return SaveResult.Failure<IdentityResourceId>(structuralError);
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(resource.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<IdentityResourceId>(extendedPropertiesError);
        }

        var idValue = id.Value;
        var existing = await repository.TryReadByIdAsync(idValue, ct);
        if (existing is null)
        {
            return SaveResult.Failure<IdentityResourceId>(StorageError.NotFound("identity_resource", idValue.ToString()));
        }

        var dso = MapToDso(idValue, resource.Name, resource.Enabled, resource.DisplayName, resource.Description, resource.ShowInDiscoveryDocument, resource.Required, resource.Emphasize, resource.UserClaims, resource.ExtendedProperties);

        var previousName = existing.Value.Dso.Name;
        if (!string.Equals(previousName, resource.Name, StringComparison.Ordinal)
            && await apiScopeRepository.TryReadByNameAsync(resource.Name, ct) is not null)
        {
            return SaveResult.Failure<IdentityResourceId>(StorageError.AlreadyExists("api_scope", resource.Name));
        }

        var result = await repository.UpdateAsync(Duende.Storage.UuidV7.From(idValue), dso, expectedVersion.Value, existing.Value.Dso.Name, ct);

        return result.Kind switch
        {
            ScopeNameBatchOutcomeKind.Success => SaveResult.Success<IdentityResourceId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
            ScopeNameBatchOutcomeKind.UnexpectedVersion => SaveResult.Failure<IdentityResourceId>(StorageError.VersionConflict()),
            ScopeNameBatchOutcomeKind.DoesNotExist => SaveResult.Failure<IdentityResourceId>(StorageError.NotFound("identity_resource", idValue.ToString())),
            ScopeNameBatchOutcomeKind.AlreadyExists =>
                SaveResult.Failure<IdentityResourceId>(StorageError.AlreadyExists("identity_resource", resource.Name)),
            ScopeNameBatchOutcomeKind.NameTakenByOtherKind =>
                SaveResult.Failure<IdentityResourceId>(StorageError.AlreadyExists("api_scope", resource.Name)),
            _ => throw new InvalidOperationException($"Unexpected ScopeNameBatchOutcomeKind: {result.Kind}")
        };
    }

    public async Task<SaveResult<IdentityResourceId>> DeleteAsync(IdentityResourceId id, Ct ct)
    {
        var idValue = id.Value;
        var result = await repository.DeleteAsync(idValue, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success<IdentityResourceId>(idValue, (DataVersion)(0)),
            DeleteResult.StillLinked => SaveResult.Failure<IdentityResourceId>(await BuildInUseErrorAsync(idValue, ct)),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    private async Task<StorageError> BuildInUseErrorAsync(Guid identityResourceId, Ct ct)
    {
        var fragments = new List<string>();

        var clients = await clientRepository.QueryByAllowedIdentityResourceIdAsync(identityResourceId, StorageDataRange.FromPage(1, MaxResourcesInError), ct);
        AppendUsageFragment(fragments, "clients", clients, c => c.ClientId);

        var samlServiceProviders = await samlServiceProviderRepository.QueryByAllowedIdentityResourceIdAsync(identityResourceId, StorageDataRange.FromPage(1, MaxResourcesInError), ct);
        AppendUsageFragment(fragments, "SAML service providers", samlServiceProviders, sp => sp.EntityId);

        if (fragments.Count == 0)
        {
            return StorageError.ValidationFailed(
                "The identity resource is still in use. Remove its references before deleting it.");
        }

        return StorageError.ValidationFailed(
            $"The identity resource is used by {string.Join(" and ", fragments)}. Remove it from them before deleting it.");
    }

    /// <summary>
    /// Appends a "&lt;label&gt; 'name1', 'name2' and N more" fragment to <paramref name="fragments"/> for a
    /// page of entities that are using the identity resource being deleted, if the page is non-empty.
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

    public async Task<QueryResult<IdentityResourceListItem>> QueryAsync(QueryRequest<IdentityResourceFilter, IdentityResourceSortField> request, Ct ct)
    {
        var result = await StorageMapping.ExecuteQueryAsync(() => repository.QueryAsync(request.ToStorage(), ct));
        return result.ConvertTo(MapToListItem).ToModel(request.Range);
    }

    private async Task<StorageError?> ValidateExtendedPropertiesAsync(AttributeValueCollection extendedProperties, Ct ct)
    {
        if (extendedProperties.Count == 0)
        {
            return null;
        }

        var schema = await schemaStore.GetAsync(SchemaId.IdentityResource, ct);

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

    private static IdentityResourceDso.V1 MapToDso(
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

    private static IdentityResourceConfiguration MapToConfiguration(IdentityResourceDso.V1 dso, IReadOnlyAttributeSchema? schema) =>
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
