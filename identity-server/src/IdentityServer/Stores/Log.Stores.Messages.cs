// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CallingIntoClientConfigurationValidatorValidatorType),
        Message = "Calling into client configuration validator: {ValidatorType}")]
    internal static partial void CallingIntoClientConfigurationValidatorValidatorType(this ILogger logger, object validatorType);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientConfigurationValidationForClientClientIdSucceeded),
        Message = "client configuration validation for client {ClientId} succeeded.")]
    internal static partial void ClientConfigurationValidationForClientClientIdSucceeded(this ILogger logger, object clientId);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(InvalidClientConfigurationForClientClientIdErrorMessage),
        Message = "Invalid client configuration for client {ClientId}: {ErrorMessage}")]
    internal static partial void InvalidClientConfigurationForClientClientIdErrorMessage(this ILogger logger, object clientId, object errorMessage);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CallingIntoClientConfigurationValidatorValidatorType2),
        Message = "Calling into client configuration validator: {ValidatorType}")]
    internal static partial void CallingIntoClientConfigurationValidatorValidatorType2(this ILogger logger, object validatorType);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientConfigurationValidationForClientClientIdSucceeded2),
        Message = "client configuration validation for client {ClientId} succeeded.")]
    internal static partial void ClientConfigurationValidationForClientClientIdSucceeded2(this ILogger logger, object clientId);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(InvalidClientConfigurationForClientClientIdErrorMessage2),
        Message = "Invalid client configuration for client {ClientId}: {ErrorMessage}")]
    internal static partial void InvalidClientConfigurationForClientClientIdErrorMessage2(this ILogger logger, object clientId, object errorMessage);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CallingIntoSAMLServiceProviderConfigurationValidatorValidatorType),
        Message = "Calling into SAML service provider configuration validator: {ValidatorType}")]
    internal static partial void CallingIntoSAMLServiceProviderConfigurationValidatorValidatorType(this ILogger logger, object validatorType);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SAMLServiceProviderConfigurationValidationForEntityIdSucceeded),
        Message = "SAML service provider configuration validation for {EntityId} succeeded.")]
    internal static partial void SAMLServiceProviderConfigurationValidationForEntityIdSucceeded(this ILogger logger, object entityId);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(InvalidSAMLServiceProviderConfigurationForEntityIdErrorMessage),
        Message = "Invalid SAML service provider configuration for {EntityId}: {ErrorMessage}")]
    internal static partial void InvalidSAMLServiceProviderConfigurationForEntityIdErrorMessage(this ILogger logger, object entityId, object errorMessage);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CallingIntoSAMLServiceProviderConfigurationValidatorValidatorType2),
        Message = "Calling into SAML service provider configuration validator: {ValidatorType}")]
    internal static partial void CallingIntoSAMLServiceProviderConfigurationValidatorValidatorType2(this ILogger logger, object validatorType);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SAMLServiceProviderConfigurationValidationForEntityIdSucceeded2),
        Message = "SAML service provider configuration validation for {EntityId} succeeded.")]
    internal static partial void SAMLServiceProviderConfigurationValidationForEntityIdSucceeded2(this ILogger logger, object entityId);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(InvalidSAMLServiceProviderConfigurationForEntityIdErrorMessage2),
        Message = "Invalid SAML service provider configuration for {EntityId}: {ErrorMessage}")]
    internal static partial void InvalidSAMLServiceProviderConfigurationForEntityIdErrorMessage2(this ILogger logger, object entityId, object errorMessage);
}
