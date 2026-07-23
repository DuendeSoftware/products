// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.DeviceFlow;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(DeviceAuthorizationStoreFailed),
        Message = $"Failed to store device authorization code: {{{LogParameters.Result}}}")]
    internal static partial void DeviceAuthorizationStoreFailed(this ILogger logger, LogLevel logLevel, string result);

    [LoggerMessage(
        EventName = nameof(UserCodeNotFound),
        Message = $"User code {{{LogParameters.UserCode}}} not found in store")]
    internal static partial void UserCodeNotFound(this ILogger logger, LogLevel logLevel, string userCode);

    [LoggerMessage(
        EventName = nameof(DeviceCodeNotFound),
        Message = $"Device code {{{LogParameters.DeviceCode}}} not found in store")]
    internal static partial void DeviceCodeNotFound(this ILogger logger, LogLevel logLevel, string deviceCode);

    [LoggerMessage(
        EventName = nameof(UserCodeNotFoundForUpdate),
        Message = $"User code {{{LogParameters.UserCode}}} not found in store for update")]
    internal static partial void UserCodeNotFoundForUpdate(this ILogger logger, LogLevel logLevel, string userCode);

    [LoggerMessage(
        EventName = nameof(DeviceCodeDeletedDuringUpdate),
        Message = $"Device code for user code {{{LogParameters.UserCode}}} was deleted during update")]
    internal static partial void DeviceCodeDeletedDuringUpdate(this ILogger logger, LogLevel logLevel, string userCode);

    [LoggerMessage(
        EventName = nameof(DeviceCodeConcurrencyConflict),
        Message = $"Concurrency conflict updating device code for user code {{{LogParameters.UserCode}}}")]
    internal static partial void DeviceCodeConcurrencyConflict(this ILogger logger, LogLevel logLevel, string userCode);

    [LoggerMessage(
        EventName = nameof(RemovingDeviceCode),
        Message = $"Removing device code {{{LogParameters.DeviceCode}}} from store")]
    internal static partial void RemovingDeviceCode(this ILogger logger, LogLevel logLevel, string deviceCode);
}

internal static class LogParameters
{
    public const string Result = "Result";
    public const string UserCode = "UserCode";
    public const string DeviceCode = "DeviceCode";
}
