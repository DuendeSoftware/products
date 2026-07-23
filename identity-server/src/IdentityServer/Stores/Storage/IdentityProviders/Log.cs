// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(CreatingProvider),
        Message = $"Creating identity provider with scheme {{{LogParameters.Scheme}}}")]
    internal static partial void CreatingProvider(this ILogger logger, LogLevel logLevel, string scheme);

    [LoggerMessage(
        EventName = nameof(ProviderCreated),
        Message = $"Identity provider created with id {{{LogParameters.ProviderId}}} and scheme {{{LogParameters.Scheme}}}")]
    internal static partial void ProviderCreated(this ILogger logger, LogLevel logLevel, Guid providerId, string scheme);

    [LoggerMessage(
        EventName = nameof(ProviderAlreadyExists),
        Message = $"Identity provider with scheme {{{LogParameters.Scheme}}} already exists")]
    internal static partial void ProviderAlreadyExists(this ILogger logger, LogLevel logLevel, string scheme);

    [LoggerMessage(
        EventName = nameof(UpdatingProvider),
        Message = $"Updating identity provider {{{LogParameters.ProviderId}}}")]
    internal static partial void UpdatingProvider(this ILogger logger, LogLevel logLevel, Guid providerId);

    [LoggerMessage(
        EventName = nameof(ProviderUpdated),
        Message = $"Identity provider {{{LogParameters.ProviderId}}} updated successfully")]
    internal static partial void ProviderUpdated(this ILogger logger, LogLevel logLevel, Guid providerId);

    [LoggerMessage(
        EventName = nameof(DeletingProvider),
        Message = $"Deleting identity provider {{{LogParameters.ProviderId}}}")]
    internal static partial void DeletingProvider(this ILogger logger, LogLevel logLevel, Guid providerId);

    [LoggerMessage(
        EventName = nameof(ProviderNotFound),
        Message = $"Identity provider {{{LogParameters.ProviderId}}} not found")]
    internal static partial void ProviderNotFound(this ILogger logger, LogLevel logLevel, Guid providerId);

    [LoggerMessage(
        EventName = nameof(ProviderSchemeNotFound),
        Message = $"Identity provider with scheme {{{LogParameters.Scheme}}} not found")]
    internal static partial void ProviderSchemeNotFound(this ILogger logger, LogLevel logLevel, string scheme);

    [LoggerMessage(
        EventName = nameof(StructuralValidationFailed),
        Message = $"Structural validation failed for identity provider: {{{LogParameters.FieldName}}} is required")]
    internal static partial void StructuralValidationFailed(this ILogger logger, LogLevel logLevel, string fieldName);

    [LoggerMessage(
        EventName = nameof(ConfigurationValidationFailed),
        Message = $"Configuration validation failed for identity provider with scheme {{{LogParameters.Scheme}}}: {{{LogParameters.ErrorMessage}}}")]
    internal static partial void ConfigurationValidationFailed(this ILogger logger, LogLevel logLevel, string scheme, string errorMessage);

    [LoggerMessage(
        EventName = nameof(VersionConflict),
        Message = $"Version conflict updating identity provider {{{LogParameters.ProviderId}}}")]
    internal static partial void VersionConflict(this ILogger logger, LogLevel logLevel, Guid providerId);

    [LoggerMessage(
        EventName = nameof(QueryingProviders),
        Message = "Querying identity providers")]
    internal static partial void QueryingProviders(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(IdentityProviderMappingFailed),
        Message = $"Identity provider record found in database, but mapping failed for scheme {{{LogParameters.Scheme}}} and protocol type {{{LogParameters.ProtocolType}}}")]
    internal static partial void IdentityProviderMappingFailed(this ILogger logger, LogLevel logLevel, string scheme, string protocolType);

    [LoggerMessage(
        EventName = nameof(SchemesRetrieved),
        Message = $"Retrieved {{{LogParameters.SchemeCount}}} identity provider scheme names")]
    internal static partial void SchemesRetrieved(this ILogger logger, LogLevel logLevel, int schemeCount);
}

internal static class LogParameters
{
    public const string Scheme = "Scheme";
    public const string ProviderId = "ProviderId";
    public const string FieldName = "FieldName";
    public const string ErrorMessage = "ErrorMessage";
    public const string ProtocolType = "ProtocolType";
    public const string SchemeCount = "SchemeCount";
}
