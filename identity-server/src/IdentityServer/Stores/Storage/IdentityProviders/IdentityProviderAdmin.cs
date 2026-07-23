// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class IdentityProviderAdmin(
    IdentityProviderRepository repository,
    IIdentityProviderConfigurationValidator validator,
    IIdentityProviderFactory identityProviderFactory,
    ILogger<IdentityProviderAdmin> logger) : IIdentityProviderAdmin
{
    public async Task<SaveResult<Guid>> CreateAsync(IdentityProviderConfiguration provider, Ct ct)
    {
        logger.CreatingProvider(LogLevel.Debug, provider.Scheme);

        var structuralError = ValidateStructure(provider);
        if (structuralError is not null)
        {
            return structuralError;
        }

        var validationError = await RunValidatorAsync(provider, ct);
        if (validationError is not null)
        {
            return validationError;
        }

        var id = UuidV7.New();
        var dso = MapToDso(id.Value, provider);

        var result = await repository.CreateAsync(id, dso, ct);

        return result switch
        {
            CreateResult.Success => LogAndReturn(
                SaveResult.Success(id.Value, (DataVersion)1),
                () => logger.ProviderCreated(LogLevel.Debug, id.Value, provider.Scheme)),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                LogAndReturn<SaveResult<Guid>>(
                    AdminError.AlreadyExists("identity_provider", provider.Scheme),
                    () => logger.ProviderAlreadyExists(LogLevel.Warning, provider.Scheme)),
            _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
        };
    }

    public async Task<GetResult<IdentityProviderConfiguration>> GetAsync(Guid id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id, ct);
        if (result is null)
        {
            logger.ProviderNotFound(LogLevel.Debug, id);
            return GetResult.NotFound<IdentityProviderConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
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
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<SaveResult<Guid>> UpdateAsync(Guid id, IdentityProviderConfiguration provider, DataVersion expectedVersion, Ct ct)
    {
        logger.UpdatingProvider(LogLevel.Debug, id);

        var structuralError = ValidateStructure(provider);
        if (structuralError is not null)
        {
            return structuralError;
        }

        var existing = await repository.TryReadByIdAsync(id, ct);
        if (existing is null)
        {
            logger.ProviderNotFound(LogLevel.Warning, id);
            return AdminError.NotFound("identity_provider", id.ToString());
        }

        var validationError = await RunValidatorAsync(provider, ct);
        if (validationError is not null)
        {
            return validationError;
        }

        var dso = MapToDso(id, provider);
        var result = await repository.UpdateAsync(UuidV7.From(id), dso, expectedVersion.Value, ct);

        return result switch
        {
            UpdateResult.Success => LogAndReturn(
                SaveResult.Success(id, (DataVersion)(expectedVersion.Value + 1)),
                () => logger.ProviderUpdated(LogLevel.Debug, id)),
            UpdateResult.UnexpectedVersion => LogAndReturn<SaveResult<Guid>>(
                AdminError.VersionConflict(),
                () => logger.VersionConflict(LogLevel.Warning, id)),
            UpdateResult.DoesNotExist => LogAndReturn<SaveResult<Guid>>(
                AdminError.NotFound("identity_provider", id.ToString()),
                () => logger.ProviderNotFound(LogLevel.Warning, id)),
            UpdateResult.KeyConflict => LogAndReturn<SaveResult<Guid>>(
                AdminError.AlreadyExists("identity_provider", provider.Scheme),
                () => logger.ProviderAlreadyExists(LogLevel.Warning, provider.Scheme)),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct)
    {
        logger.DeletingProvider(LogLevel.Debug, id);

        var result = await repository.DeleteAsync(id, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success(id, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<Duende.Storage.Querying.QueryResult<IdentityProviderListItem>> QueryAsync(QueryRequest<IdentityProviderFilter, IdentityProviderSortField> request, Ct ct)
    {
        logger.QueryingProviders(LogLevel.Debug);

        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    private static AdminError? ValidateStructure(IdentityProviderConfiguration provider)
    {
        if (string.IsNullOrWhiteSpace(provider.Scheme))
        {
            return AdminError.Required("Scheme");
        }

        if (string.IsNullOrWhiteSpace(provider.Type))
        {
            return AdminError.Required("Type");
        }

        return null;
    }

    private async Task<AdminError?> RunValidatorAsync(IdentityProviderConfiguration provider, Ct ct)
    {
        var baseProvider = MapToIsProvider(provider);
        var typedProvider = identityProviderFactory.Create(baseProvider) ?? baseProvider;
        var context = new IdentityProviderConfigurationValidationContext(typedProvider);
        await validator.ValidateAsync(context, ct);

        if (!context.IsValid)
        {
            var message = context.ErrorMessage ?? "Identity provider configuration validation failed.";
            logger.ConfigurationValidationFailed(LogLevel.Warning, provider.Scheme, message);
            return AdminError.ValidationFailed(message);
        }

        return null;
    }

    private static IdentityProvider MapToIsProvider(IdentityProviderConfiguration provider) =>
        new(provider.Type)
        {
            Scheme = provider.Scheme,
            DisplayName = provider.DisplayName,
            Enabled = provider.Enabled,
            Properties = provider.Properties is not null
                ? new Dictionary<string, string>(provider.Properties)
                : new Dictionary<string, string>()
        };

    private static IdentityProviderDso.V1 MapToDso(Guid id, IdentityProviderConfiguration provider) =>
        new()
        {
            Id = id,
            Scheme = provider.Scheme,
            DisplayName = provider.DisplayName,
            Enabled = provider.Enabled,
            Type = provider.Type,
            Properties = provider.Properties is not null
                ? new Dictionary<string, string>(provider.Properties)
                : new Dictionary<string, string>()
        };

    private static IdentityProviderConfiguration MapToConfiguration(IdentityProviderDso.V1 dso) =>
        new()
        {
            Scheme = dso.Scheme,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Type = dso.Type,
            Properties = new Dictionary<string, string>(dso.Properties)
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
