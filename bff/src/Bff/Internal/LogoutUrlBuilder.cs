// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.Internal;

/// <summary>
/// Class that helps to construct logout urls
/// </summary>
internal static class LogoutUrlBuilder
{
    /// <summary>
    /// Builds a logout url that includes the session id as a query string parameter
    /// 
    /// </summary>
    /// <param name="basePath"></param>
    /// <param name="options"></param>
    /// <param name="sessionId"></param>
    internal static Uri Build(PathString basePath, BffOptions options, string? sessionId)
    {
        string logoutUrl = basePath + options.LogoutPath;
        if (sessionId != null)
        {
            logoutUrl += $"?sid={UrlEncoder.Default.Encode(sessionId)}";
        }

        return new Uri(logoutUrl, UriKind.Relative);
    }

    internal static Uri Build(NavigationManager navigation, BffOptions options, string? sessionId)
    {
        PathString prefix = navigation.ToBaseRelativePath(navigation.BaseUri);
        return Build(prefix, options, sessionId);
    }
}
