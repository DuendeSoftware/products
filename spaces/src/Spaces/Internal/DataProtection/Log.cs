// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Spaces.Internal.DataProtection;

internal static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Unprotected a value with the unpartitioned purpose chain in the Default space.")]
    public static partial void DefaultSpaceFallback(this ILogger logger);
}
