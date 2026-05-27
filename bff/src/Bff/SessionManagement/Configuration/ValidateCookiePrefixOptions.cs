// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Validates that cookie authentication options are consistent with the RFC 6265bis
/// requirements for __Host- and __Secure- cookie name prefixes.
/// </summary>
internal sealed class ValidateCookiePrefixOptions(
    ActiveCookieAuthenticationScheme activeCookieScheme)
    : IValidateOptions<CookieAuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, CookieAuthenticationOptions options)
    {
        if (!activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            return ValidateOptionsResult.Skip;
        }

        var cookieName = options.Cookie.Name;

        if (cookieName?.StartsWith(Constants.Cookies.HostPrefix, StringComparison.Ordinal) == true)
        {
            if (options.Cookie.SecurePolicy == CookieSecurePolicy.None)
            {
                return ValidateOptionsResult.Fail(
                    $"Cookie '{cookieName}' uses the __Host- prefix which requires the Secure attribute. " +
                    $"Set Cookie.SecurePolicy to Always or SameAsRequest.");
            }

            if (options.Cookie.Domain != null)
            {
                return ValidateOptionsResult.Fail(
                    $"Cookie '{cookieName}' uses the __Host- prefix which must not have a Domain attribute. " +
                    $"Remove the Cookie.Domain setting.");
            }

            if (options.Cookie.Path != null && options.Cookie.Path != "/")
            {
                return ValidateOptionsResult.Fail(
                    $"Cookie '{cookieName}' uses the __Host- prefix which requires Path=\"/\". " +
                    $"Remove the Cookie.Path setting or set it to \"/\".");
            }
        }
        else if (cookieName?.StartsWith(Constants.Cookies.SecurePrefix, StringComparison.Ordinal) == true)
        {
            if (options.Cookie.SecurePolicy == CookieSecurePolicy.None)
            {
                return ValidateOptionsResult.Fail(
                    $"Cookie '{cookieName}' uses the __Secure- prefix which requires the Secure attribute. " +
                    $"Set Cookie.SecurePolicy to Always or SameAsRequest.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
