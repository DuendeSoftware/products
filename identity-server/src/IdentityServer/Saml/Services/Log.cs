// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Services;

internal static class LogParameters
{
    public const string EntityId = "entityId";
    public const string Count = "count";
    public const string StateIdParam = "StateIdParam";
    public const string StateId = "StateId";
}

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(NoSamlServiceProvidersToNotifyForLogout),
        Message = "No SAML Service Providers to notify for logout")]
    internal static partial void NoSamlServiceProvidersToNotifyForLogout(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(SkippingLogoutUrlGenerationForUnknownOrDisabledServiceProvider),
        Message = $"Skipping SAML logout for disabled or unknown SP: {{{LogParameters.EntityId}}}")]
    internal static partial void SkippingLogoutUrlGenerationForUnknownOrDisabledServiceProvider(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SkippingLogoutUrlGenerationForServiceProviderWithNoSingleLogout),
        Message = $"Skipping SAML logout for SP without any SingleLogoutServiceUrl: {{{LogParameters.EntityId}}}")]
    internal static partial void SkippingLogoutUrlGenerationForServiceProviderWithNoSingleLogout(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SkippingLogoutUrlGenerationForUnsupportedBinding),
        Message = $"Skipping SAML logout for SP with unsupported binding (only HTTP-Redirect is supported for front-channel): {{{LogParameters.EntityId}}}, Binding: {{binding}}")]
    internal static partial void SkippingLogoutUrlGenerationForUnsupportedBinding(this ILogger logger, LogLevel logLevel, string entityId, SamlBinding binding);

    [LoggerMessage(
        EventName = nameof(FailedToGenerateLogoutUrlForServiceProvider),
        Level = LogLevel.Error,
        Message = $"Failed to build SAML logout URL for SP: {{{LogParameters.EntityId}}}")]
    internal static partial void FailedToGenerateLogoutUrlForServiceProvider(this ILogger logger, Exception ex, string entityId);

    [LoggerMessage(
        EventName = nameof(GeneratedSamlFrontChannelLogoutUrls),
        Message = $"Generated {{{LogParameters.Count}}} SAML front-channel logout URLs")]
    internal static partial void GeneratedSamlFrontChannelLogoutUrls(this ILogger logger, LogLevel logLevel, int count);

    [LoggerMessage(
        EventName = nameof(NoSamlFrontChannelLogoutUrlsGenerated),
        Message = "No SAML front-channel logout URLs generated")]
    internal static partial void NoSamlFrontChannelLogoutUrlsGenerated(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(SkippingLogoutNotificationForInitiatingServiceProvider),
        Message = $"Skipping front-channel logout notification for initiating SP (will receive LogoutResponse instead): {{{LogParameters.EntityId}}}")]
    internal static partial void SkippingLogoutNotificationForInitiatingServiceProvider(this ILogger logger, LogLevel logLevel, string entityId);

    // SamlReturnUrlParser

    [LoggerMessage(
        EventName = nameof(ReturnUrlIsNotLocalUrl),
        Message = "returnUrl is not a local URL")]
    internal static partial void ReturnUrlIsNotLocalUrl(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ReturnUrlDoesNotMatchSamlCallbackPath),
        Message = "returnUrl does not match SAML callback path")]
    internal static partial void ReturnUrlDoesNotMatchSamlCallbackPath(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ReturnUrlHasNoQueryString),
        Message = "returnUrl has no query string")]
    internal static partial void ReturnUrlHasNoQueryString(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ReturnUrlMissingStateIdParam),
        Message = $"returnUrl is missing {{{LogParameters.StateIdParam}}} query parameter")]
    internal static partial void ReturnUrlMissingStateIdParam(this ILogger logger, LogLevel logLevel, string stateIdParam);

    [LoggerMessage(
        EventName = nameof(ReturnUrlIsValidSamlCallbackUrl),
        Message = "returnUrl is a valid SAML callback URL")]
    internal static partial void ReturnUrlIsValidSamlCallbackUrl(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(NoSamlAuthenticationContextBeingReturned),
        Message = "No SamlAuthenticationContext being returned")]
    internal static partial void NoSamlAuthenticationContextBeingReturned(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(CouldNotParseStateIdParamAsGuid),
        Message = $"Could not parse {{{LogParameters.StateIdParam}}} as a Guid")]
    internal static partial void CouldNotParseStateIdParamAsGuid(this ILogger logger, LogLevel logLevel, string stateIdParam);

    [LoggerMessage(
        EventName = nameof(NoSamlStateFoundForStateId),
        Message = $"No SAML state found for stateId {{{LogParameters.StateId}}}")]
    internal static partial void NoSamlStateFoundForStateId(this ILogger logger, LogLevel logLevel, Guid stateId);

    [LoggerMessage(
        EventName = nameof(NoServiceProviderFoundForEntityId),
        Message = $"No service provider found for entity ID {{{LogParameters.EntityId}}}")]
    internal static partial void NoServiceProviderFoundForEntityId(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SamlAuthenticationContextBeingReturned),
        Message = "SamlAuthenticationContext being returned")]
    internal static partial void SamlAuthenticationContextBeingReturned(this ILogger logger, LogLevel logLevel);
}
