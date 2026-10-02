// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(FailedToConfigureTheDynamicAuthenticationSchemeScheme),
        Message = "Failed to configure the dynamic authentication scheme \"{Scheme}\" because there is no current HTTP request.")]
    internal static partial void FailedToConfigureTheDynamicAuthenticationSchemeScheme(this ILogger logger, object scheme);
}
