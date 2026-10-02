// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints.Results;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(UnsupportedSAMLBinding),
        Level = LogLevel.Debug,
        Message = "Unsupported SAML Binding: {Binding}")]
    internal static partial void UnsupportedSAMLBinding(this ILogger logger, object? binding);
}
