// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Services.Default;

internal static class LogParameters
{
    public const string SpEntityId = "SpEntityId";
    public const string Scope = "Scope";
    public const string ClaimType = "ClaimType";
    public const string AcsUrl = "AcsUrl";
    public const string MaxRelayStateLength = "MaxRelayStateLength";
    public const string Error = "Error";
}

internal static partial class Log
{
    // DefaultSamlResourceResolver

    [LoggerMessage(
        EventName = nameof(ServiceProviderHasNoAllowedScopes),
        Level = LogLevel.Error,
        Message = $"Service provider {{{LogParameters.SpEntityId}}} has no AllowedScopes configured")]
    internal static partial void ServiceProviderHasNoAllowedScopes(this ILogger logger, string spEntityId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderHasInvalidAllowedScope),
        Level = LogLevel.Error,
        Message = $"Service provider {{{LogParameters.SpEntityId}}} has AllowedScope '{{{LogParameters.Scope}}}' that does not match any enabled identity resource")]
    internal static partial void ServiceProviderHasInvalidAllowedScope(this ILogger logger, string spEntityId, string scope);

    [LoggerMessage(
        EventName = nameof(ServiceProviderHasInvalidRequestedClaimType),
        Level = LogLevel.Error,
        Message = $"Service provider {{{LogParameters.SpEntityId}}} has RequestedClaimType '{{{LogParameters.ClaimType}}}' that is not in any identity resource from AllowedScopes")]
    internal static partial void ServiceProviderHasInvalidRequestedClaimType(this ILogger logger, string spEntityId, string claimType);

    // DefaultIdpInitiatedSsoService

    [LoggerMessage(
        EventName = nameof(IdpInitiatedSsoMissingSpEntityId),
        Message = "IdP-initiated SSO request missing required 'spEntityId' parameter")]
    internal static partial void IdpInitiatedSsoMissingSpEntityId(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ServiceProviderNotFound),
        Message = $"Service provider '{{{LogParameters.SpEntityId}}}' not found")]
    internal static partial void ServiceProviderNotFound(this ILogger logger, LogLevel logLevel, string spEntityId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderIsDisabled),
        Message = $"Service provider '{{{LogParameters.SpEntityId}}}' is disabled")]
    internal static partial void ServiceProviderIsDisabled(this ILogger logger, LogLevel logLevel, string spEntityId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderDoesNotAllowIdpInitiatedSso),
        Message = $"Service provider '{{{LogParameters.SpEntityId}}}' does not allow IdP-initiated SSO")]
    internal static partial void ServiceProviderDoesNotAllowIdpInitiatedSso(this ILogger logger, LogLevel logLevel, string spEntityId);

    [LoggerMessage(
        EventName = nameof(RelayStateExceedsMaxLength),
        Message = $"RelayState exceeds maximum length of {{{LogParameters.MaxRelayStateLength}}} bytes")]
    internal static partial void RelayStateExceedsMaxLength(this ILogger logger, LogLevel logLevel, int maxRelayStateLength);

    [LoggerMessage(
        EventName = nameof(ServiceProviderHasNoAssertionConsumerServiceUrls),
        Message = $"Service provider '{{{LogParameters.SpEntityId}}}' has no assertion consumer service URLs configured")]
    internal static partial void ServiceProviderHasNoAssertionConsumerServiceUrls(this ILogger logger, LogLevel logLevel, string spEntityId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderHasInvalidAcsUrl),
        Level = LogLevel.Error,
        Message = $"Service provider '{{{LogParameters.SpEntityId}}}' has an invalid ACS URL: {{{LogParameters.AcsUrl}}}")]
    internal static partial void ServiceProviderHasInvalidAcsUrl(this ILogger logger, string spEntityId, string acsUrl);

    [LoggerMessage(
        EventName = nameof(UserIsNotAuthenticated),
        Message = "User is not authenticated")]
    internal static partial void UserIsNotAuthenticated(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ResourceResolutionFailed),
        Level = LogLevel.Error,
        Message = $"Resource resolution failed for service provider '{{{LogParameters.SpEntityId}}}': {{{LogParameters.Error}}}")]
    internal static partial void ResourceResolutionFailed(this ILogger logger, string spEntityId, string? error);

    [LoggerMessage(
        EventName = nameof(ResponseGeneratorReturnedNoMessage),
        Level = LogLevel.Error,
        Message = $"Response generator returned a result with no message for SP '{{{LogParameters.SpEntityId}}}'")]
    internal static partial void ResponseGeneratorReturnedNoMessage(this ILogger logger, string spEntityId);
}
