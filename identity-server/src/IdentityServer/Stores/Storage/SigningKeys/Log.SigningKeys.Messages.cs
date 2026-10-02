// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.SigningKeys;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(FailedToStoreSigningKeyKeyIdResult),
        Message = "Failed to store signing key {KeyId}: {Result}")]
    internal static partial void FailedToStoreSigningKeyKeyIdResult(this ILogger logger, object keyId, object result);
}
