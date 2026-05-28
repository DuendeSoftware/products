// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Saml.Infrastructure;

internal static class SamlLogParameters
{
    internal const string SecurityKey = "securityKey";
}

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(SigningCredentialIsNotX509Certificate),
        Message = $"Signing credential is not an X509 certificate (Key: {{{SamlLogParameters.SecurityKey}}}). SAML signing requires X509 certificates with private keys.")]
    internal static partial void SigningCredentialIsNotX509Certificate(this ILogger logger, LogLevel level, SecurityKey securityKey);
}
