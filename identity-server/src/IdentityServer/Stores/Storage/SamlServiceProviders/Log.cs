// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.SamlServiceProviders;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(SamlServiceProviderFound),
        Message = $"{{{LogParameters.EntityId}}} found in database")]
    internal static partial void SamlServiceProviderFound(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SamlServiceProviderNotFound),
        Message = $"{{{LogParameters.EntityId}}} not found in database")]
    internal static partial void SamlServiceProviderNotFound(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SamlServiceProvidersRetrieved),
        Message = $"Retrieved {{{LogParameters.Count}}} SAML Service Providers for enumeration")]
    internal static partial void SamlServiceProvidersRetrieved(this ILogger logger, LogLevel logLevel, int count);
}

internal static class LogParameters
{
    public const string EntityId = "EntityId";
    public const string Count = "Count";
}
