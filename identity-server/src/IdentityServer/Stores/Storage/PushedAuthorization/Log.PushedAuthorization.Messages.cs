// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.PushedAuthorization;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(FailedToStorePushedAuthorizationRequestHashResult),
        Message = "Failed to store pushed authorization request {Hash}: {Result}")]
    internal static partial void FailedToStorePushedAuthorizationRequestHashResult(this ILogger logger, object hash, object result);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(PushedAuthorizationRequestHashNotFoundInStore),
        Message = "Pushed authorization request {Hash} not found in store")]
    internal static partial void PushedAuthorizationRequestHashNotFoundInStore(this ILogger logger, object hash);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ConsumingPushedAuthorizationRequestHash),
        Message = "Consuming pushed authorization request {Hash}")]
    internal static partial void ConsumingPushedAuthorizationRequestHash(this ILogger logger, object hash);
}
