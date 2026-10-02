// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(TheAuthenticationHandlerOptionsForSchemeSchemeWere),
        Message = "The authentication handler options for scheme {Scheme} were evicted because the identity provider configuration changed. Consider enabling caching for the IIdentityProviderStore with AddIdentityProviderStoreCache<T>() on IdentityServer if you do not want the options to be reinitialized on each request.")]
    internal static partial void TheAuthenticationHandlerOptionsForSchemeSchemeWere(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CallingIntoIdentityProviderConfigurationValidatorValidatorType),
        Message = "Calling into identity provider configuration validator: {ValidatorType}")]
    internal static partial void CallingIntoIdentityProviderConfigurationValidatorValidatorType(this ILogger logger, object validatorType);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(IdentityProviderValidationForSchemeSchemeSucceeded),
        Message = "IdentityProvider validation for scheme {Scheme} succeeded.")]
    internal static partial void IdentityProviderValidationForSchemeSchemeSucceeded(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(InvalidIdentityProviderConfigurationForSchemeSchemeErrorMessage),
        Message = "Invalid IdentityProvider configuration for scheme {Scheme}: {ErrorMessage}")]
    internal static partial void InvalidIdentityProviderConfigurationForSchemeSchemeErrorMessage(this ILogger logger, object scheme, object errorMessage);
}
