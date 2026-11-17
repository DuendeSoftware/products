// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Scim.Internal;

internal sealed partial class ScimLocalIssuerConfigurationManager
{
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Debug,
            EventName = nameof(ResolvedLocalScimIssuer),
            Message = "Resolved SCIM issuer {Issuer} with {KeyCount} validation key(s) for the current request.")]
        internal static partial void ResolvedLocalScimIssuer(ILogger logger, string issuer, int keyCount);

        [LoggerMessage(
            LogLevel.Error,
            EventName = nameof(NoSpaceConfiguredForScimRequest),
            Message = "SCIM bearer token validation requires the current space, but none is set for this request. " +
                "Call UseSpaceResolution() before UseAuthorization() in the ASP.NET Core pipeline. Rejecting the request.")]
        internal static partial void NoSpaceConfiguredForScimRequest(ILogger logger);
    }
}
