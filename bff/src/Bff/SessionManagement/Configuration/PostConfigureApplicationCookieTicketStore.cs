// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration for the user session plumbing
/// </summary>
internal class PostConfigureApplicationCookieTicketStore(
    SelectedFrontend selectedFrontend,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AuthenticationOptions> options)
    : IPostConfigureOptions<CookieAuthenticationOptions>

{
    private readonly string? _scheme = options.Value.DefaultAuthenticateScheme ?? options.Value.DefaultScheme;

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        var isDefaultScheme = name == _scheme;
        var isForBffFrontend = selectedFrontend.TryGet(out var frontend) && name == frontend.CookieSchemeName;
        var isForImplicitConfig = BffAuthenticationSchemes.BffCookie == name;
        if (isDefaultScheme || isForBffFrontend || isForImplicitConfig)
        {
            options.SessionStore = new TicketStoreShim(httpContextAccessor);
        }
    }
}
