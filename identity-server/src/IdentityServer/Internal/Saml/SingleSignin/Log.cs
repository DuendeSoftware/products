// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal static class SingleSignInLogParameters
{
    public const string Message = "Message";
    public const string RequestId = "Id";
    public const string Issuer = "Issuer";
    public const string Format = "Format";
    public const string SPNameQualifier = "SPNameQualifier";
    public const string Source = "Source";
}

internal static partial class Log
{
    [LoggerMessage(LogLevel.Error,
        Message = $"Failed to parse AuthnRequest XML: {{{SingleSignInLogParameters.Message}}}")]
    internal static partial void FailedToParseAuthNRequest(this ILogger logger, Exception ex, string message);

    [LoggerMessage(LogLevel.Error,
        Message = "Unexpected error parsing AuthnRequest")]
    internal static partial void UnexpectedErrorParsingAuthNRequest(this ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Debug,
        Message =
            $"Parsed AuthnRequest {{{SingleSignInLogParameters.RequestId}}} from {{{SingleSignInLogParameters.Issuer}}}")]
    internal static partial void ParsedAuthenticationRequest(this ILogger logger, string id, string issuer);

    [LoggerMessage(
        EventName = nameof(NameIdPolicyParsed),
        Message = $"Parsed NameIDPolicy: Format='{{{SingleSignInLogParameters.Format}}}', SPNameQualifier='{{{SingleSignInLogParameters.SPNameQualifier}}}'")]
    internal static partial void NameIdPolicyParsed(this ILogger logger, LogLevel level, string? format, string? spNameQualifier);

    [LoggerMessage(
        EventName = nameof(RequestedNameIdFormatNotSupported),
        Message = $"Requested NameID format '{{{SingleSignInLogParameters.Format}}}' is not supported, returning InvalidNameIDPolicy error")]
    internal static partial void RequestedNameIdFormatNotSupported(this ILogger logger, LogLevel level, string format);

    [LoggerMessage(
        EventName = nameof(UsingNameIdFormat),
        Message = $"Using NameID format '{{{SingleSignInLogParameters.Format}}}'")]
    internal static partial void UsingNameIdFormat(this ILogger logger, LogLevel level, string format);
}
