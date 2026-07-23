// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.Resources;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(IdentityResourcesFound),
        Message = $"Found {{{LogParameters.Scopes}}} identity scopes in database")]
    internal static partial void IdentityResourcesFound(this ILogger logger, LogLevel logLevel, IEnumerable<string> scopes);

    [LoggerMessage(
        EventName = nameof(ApiScopesFound),
        Message = $"Found {{{LogParameters.Scopes}}} scopes in database")]
    internal static partial void ApiScopesFound(this ILogger logger, LogLevel logLevel, IEnumerable<string> scopes);

    [LoggerMessage(
        EventName = nameof(ApiResourcesFound),
        Message = $"Found {{{LogParameters.ApiResources}}} API resources in database")]
    internal static partial void ApiResourcesFound(this ILogger logger, LogLevel logLevel, IEnumerable<string> apiResources);

    [LoggerMessage(
        EventName = nameof(ApiResourcesNotFound),
        Message = $"Did not find {{{LogParameters.ApiResources}}} API resources in database")]
    internal static partial void ApiResourcesNotFound(this ILogger logger, LogLevel logLevel, IEnumerable<string> apiResources);

    [LoggerMessage(
        EventName = nameof(AllResourcesFound),
        Message = $"Found {{{LogParameters.Scopes}}} as all scopes, and {{{LogParameters.ApiResources}}} as API resources")]
    internal static partial void AllResourcesFound(this ILogger logger, LogLevel logLevel, IEnumerable<string> scopes, IEnumerable<string> apiResources);
}

internal static class LogParameters
{
    public const string Scopes = "Scopes";
    public const string ApiResources = "ApiResources";
}
