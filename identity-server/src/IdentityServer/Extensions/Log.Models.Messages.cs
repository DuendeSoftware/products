// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Models;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(GetProfileCalledForSubjectSubjectFromApplication),
        Message = "Get profile called for subject {Subject} from application {Application} with claim types {ClaimTypes} via {Caller}")]
    internal static partial void GetProfileCalledForSubjectSubjectFromApplication(this ILogger logger, object subject, object application, IEnumerable<string> claimTypes, object caller);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(IssuedClaimsClaims),
        Message = "Issued claims: {Claims}")]
    internal static partial void IssuedClaimsClaims(this ILogger logger, IEnumerable<string> claims);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ResourceIndicatorResourceIsNotAValidURI),
        Message = "Resource indicator {Resource} is not a valid URI.")]
    internal static partial void ResourceIndicatorResourceIsNotAValidURI(this ILogger logger, object resource);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ResourceIndicatorResourceMustNotContainAFragment),
        Message = "Resource indicator {Resource} must not contain a fragment component.")]
    internal static partial void ResourceIndicatorResourceMustNotContainAFragment(this ILogger logger, object resource);
}
