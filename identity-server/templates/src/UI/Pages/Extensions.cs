// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages;

public static class Extensions
{
    /// <summary>
    /// Determines if the authentication scheme support signout.
    /// </summary>
    internal static async Task<bool> GetSchemeSupportsSignOutAsync(this HttpContext context, string scheme)
    {
        var provider = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await provider.GetHandlerAsync(context, scheme);
        return (handler is IAuthenticationSignOutHandler);
    }

    /// <summary>
    /// Checks if the redirect URI is for a native client.
    /// </summary>
    internal static bool IsNativeClient(this AuthorizationRequest context)
    {
        return !context.RedirectUri.StartsWith("https", StringComparison.Ordinal)
               && !context.RedirectUri.StartsWith("http", StringComparison.Ordinal);
    }

    /// <summary>
    /// Renders a loading page that is used to redirect back to the redirectUri.
    /// </summary>
    internal static IActionResult LoadingPage(this PageModel page, string? redirectUri)
    {
        page.HttpContext.Response.StatusCode = 200;
        page.HttpContext.Response.Headers["Location"] = "";

        return page.RedirectToPage("/Redirect/Index", new { RedirectUri = redirectUri });
    }

    /// <summary>
    /// Check for a remote connection (non-localhost)
    /// </summary>
    internal static bool IsRemote(this ConnectionInfo connection)
    {
        var localAddresses = new List<string?> { "127.0.0.1", "::1" };
        if (connection.LocalIpAddress != null)
        {
            localAddresses.Add(connection.LocalIpAddress.ToString());
        }

        if (!localAddresses.Contains(connection.RemoteIpAddress?.ToString()))
        {
            return true;
        }
        return false;
    }
}