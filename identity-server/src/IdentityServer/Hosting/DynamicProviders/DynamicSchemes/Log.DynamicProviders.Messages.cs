// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(IAuthenticationSchemeProviderBeingUsedOutsideHTTPRequestThereforeDynamic),
        Message = "IAuthenticationSchemeProvider being used outside HTTP request, therefore dynamic provider feature can't be used for loading scheme: {Scheme}.")]
    internal static partial void IAuthenticationSchemeProviderBeingUsedOutsideHTTPRequestThereforeDynamic(this ILogger logger, object scheme);
}
