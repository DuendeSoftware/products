// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class IdentityProviderAdmin(
    IdentityProviderRepository repository,
    ISchemaStore schemaStore,
    IIdentityProviderConfigurationValidator validator,
    IIdentityProviderFactory identityProviderFactory,
    ILogger<IdentityProviderAdmin> logger) : IIdentityProviderAdmin
{
    public async Task<SaveResult<IdentityProviderId>> CreateAsync(CreateIdentityProvider provider, Ct ct)
    {
        logger.CreatingProvider(LogLevel.Debug, provider.Scheme);

        var structuralError = ValidateStructure(provider.Scheme, provider.Type);
        if (structuralError is not null)
        {
            return SaveResult.Failure<IdentityProviderId>(structuralError);
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(provider.Type, provider.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<IdentityProviderId>(extendedPropertiesError);
        }

        var validationError = await RunValidatorAsync(provider.Scheme, provider.DisplayName, provider.Enabled, provider.Type, provider.ExtendedProperties, ct);
        if (validationError is not null)
        {
            return SaveResult.Failure<IdentityProviderId>(validationError);
        }

        var id = UuidV7.New();
        var dso = MapToDso(id.Value, provider.Scheme, provider.DisplayName, provider.Enabled, provider.Type, provider.ExtendedProperties);

        var result = await repository.CreateAsync(id, dso, ct);

        return result switch
        {
            CreateResult.Success => LogAndReturn(
                SaveResult.Success<IdentityProviderId>(id.Value, (DataVersion)1),
                () => logger.ProviderCreated(LogLevel.Debug, id.Value, provider.Scheme)),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                LogAndReturn<SaveResult<IdentityProviderId>>(
                    SaveResult.Failure<IdentityProviderId>(StorageError.AlreadyExists("identity_provider", provider.Scheme)),
                    () => logger.ProviderAlreadyExists(LogLevel.Warning, provider.Scheme)),
            _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
        };
    }

    public async Task<GetResult<IdentityProviderConfiguration>> GetAsync(IdentityProviderId id, Ct ct)
    {
        var idValue = id.Value;
        var result = await repository.TryReadByIdAsync(idValue, ct);
        if (result is null)
        {
            logger.ProviderNotFound(LogLevel.Debug, idValue);
            return GetResult.NotFound<IdentityProviderConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.IdentityProvider(dso.Type), ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
    }

    public async Task<GetResult<IdentityProviderConfiguration>> GetBySchemeAsync(string scheme, Ct ct)
    {
        var result = await repository.TryReadBySchemeAsync(scheme, ct);
        if (result is null)
        {
            logger.ProviderSchemeNotFound(LogLevel.Debug, scheme);
            return GetResult.NotFound<IdentityProviderConfiguration>();
        }

        var (dso, version) = result.Value;
        var schema = await schemaStore.GetAsync(SchemaId.IdentityProvider(dso.Type), ct);
        return GetResult.Found(MapToConfiguration(dso, schema), (DataVersion)version);
    }

    public async Task<SaveResult<IdentityProviderId>> UpdateAsync(IdentityProviderId id, UpdateIdentityProvider provider, DataVersion expectedVersion, Ct ct)
    {
        var idValue = id.Value;
        logger.UpdatingProvider(LogLevel.Debug, idValue);

        var structuralError = ValidateStructure(provider.Scheme, provider.Type);
        if (structuralError is not null)
        {
            return SaveResult.Failure<IdentityProviderId>(structuralError);
        }

        var existing = await repository.TryReadByIdAsync(idValue, ct);
        if (existing is null)
        {
            logger.ProviderNotFound(LogLevel.Warning, idValue);
            return SaveResult.Failure<IdentityProviderId>(StorageError.NotFound("identity_provider", idValue.ToString()));
        }

        var extendedPropertiesError = await ValidateExtendedPropertiesAsync(provider.Type, provider.ExtendedProperties, ct);
        if (extendedPropertiesError is not null)
        {
            return SaveResult.Failure<IdentityProviderId>(extendedPropertiesError);
        }

        var validationError = await RunValidatorAsync(provider.Scheme, provider.DisplayName, provider.Enabled, provider.Type, provider.ExtendedProperties, ct);
        if (validationError is not null)
        {
            return SaveResult.Failure<IdentityProviderId>(validationError);
        }

        var dso = MapToDso(idValue, provider.Scheme, provider.DisplayName, provider.Enabled, provider.Type, provider.ExtendedProperties);
        var result = await repository.UpdateAsync(UuidV7.From(idValue), dso, expectedVersion.Value, ct);

        return result switch
        {
            UpdateResult.Success => LogAndReturn(
                SaveResult.Success<IdentityProviderId>(idValue, (DataVersion)(expectedVersion.Value + 1)),
                () => logger.ProviderUpdated(LogLevel.Debug, idValue)),
            UpdateResult.UnexpectedVersion => LogAndReturn<SaveResult<IdentityProviderId>>(
                SaveResult.Failure<IdentityProviderId>(StorageError.VersionConflict()),
                () => logger.VersionConflict(LogLevel.Warning, idValue)),
            UpdateResult.DoesNotExist => LogAndReturn<SaveResult<IdentityProviderId>>(
                SaveResult.Failure<IdentityProviderId>(StorageError.NotFound("identity_provider", idValue.ToString())),
                () => logger.ProviderNotFound(LogLevel.Warning, idValue)),
            UpdateResult.KeyConflict => LogAndReturn<SaveResult<IdentityProviderId>>(
                SaveResult.Failure<IdentityProviderId>(StorageError.AlreadyExists("identity_provider", provider.Scheme)),
                () => logger.ProviderAlreadyExists(LogLevel.Warning, provider.Scheme)),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<IdentityProviderId>> DeleteAsync(IdentityProviderId id, Ct ct)
    {
        var idValue = id.Value;
        logger.DeletingProvider(LogLevel.Debug, idValue);

        var result = await repository.DeleteAsync(idValue, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success<IdentityProviderId>(idValue, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<Duende.Storage.Querying.QueryResult<IdentityProviderListItem>> QueryAsync(QueryRequest<IdentityProviderFilter, IdentityProviderSortField> request, Ct ct)
    {
        logger.QueryingProviders(LogLevel.Debug);

        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    private async Task<StorageError?> ValidateExtendedPropertiesAsync(string type, AttributeValueCollection extendedProperties, Ct ct)
    {
        if (extendedProperties.Count == 0)
        {
            return null;
        }

        var schemaId = SchemaId.IdentityProvider(type);
        var schema = await schemaStore.GetAsync(schemaId, ct);

        if (!extendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return StorageError.ValidationFailed(string.Join("; ", errors));
        }

        return null;
    }

    private static StorageError? ValidateStructure(string scheme, string type)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return StorageError.Required("Scheme");
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            return StorageError.Required("Type");
        }

        return null;
    }

    private async Task<StorageError?> RunValidatorAsync(string scheme, string? displayName, bool enabled, string type, AttributeValueCollection extendedProperties, Ct ct)
    {
        var baseProvider = MapToIsProvider(scheme, displayName, enabled, type, extendedProperties);
        var typedProvider = identityProviderFactory.Create(baseProvider) ?? baseProvider;
        var context = new IdentityProviderConfigurationValidationContext(typedProvider);
        await validator.ValidateAsync(context, ct);

        if (!context.IsValid)
        {
            var message = context.ErrorMessage ?? "Identity provider configuration validation failed.";
            logger.ConfigurationValidationFailed(LogLevel.Warning, scheme, message);
            return StorageError.ValidationFailed(message);
        }

        return null;
    }

    private static IdentityProvider MapToIsProvider(string scheme, string? displayName, bool enabled, string type, AttributeValueCollection extendedProperties) =>
        new(type)
        {
            Scheme = scheme,
            DisplayName = displayName,
            Enabled = enabled,
            Properties = EavPropertyMapper.ExtractStringProperties(
                EavMapper.ToDsoList(extendedProperties))
        };

    private static IdentityProviderDso.V1 MapToDso(Guid id, string scheme, string? displayName, bool enabled, string type, AttributeValueCollection extendedProperties) =>
        new()
        {
            Id = id,
            Scheme = scheme,
            DisplayName = displayName,
            Enabled = enabled,
            Type = type,
            ExtendedAttributeValues = EavMapper.ToDsoList(extendedProperties)
        };

    private static IdentityProviderConfiguration MapToConfiguration(IdentityProviderDso.V1 dso, IReadOnlyAttributeSchema? schema) =>
        new()
        {
            Scheme = dso.Scheme,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Type = dso.Type,
            ExtendedProperties = EavMapper.ToAttributeValues(dso.ExtendedAttributeValues ?? [], schema).ToList()
        };

    private static IdentityProviderListItem MapToListItem(IdentityProviderDso.V1 dso) =>
        new()
        {
            Id = dso.Id,
            Scheme = dso.Scheme,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Type = dso.Type
        };

    private static T LogAndReturn<T>(T value, Action logAction)
    {
        logAction();
        return value;
    }
}
