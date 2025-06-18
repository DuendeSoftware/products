// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration for the user session plumbing
/// </summary>
internal class PostConfigureApplicationCookieTicketStore(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    IHttpContextAccessor httpContextAccessor)
    : IPostConfigureOptions<CookieAuthenticationOptions>

{

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            options.SessionStore = new TicketStoreShim(httpContextAccessor);
        }
    }
}
