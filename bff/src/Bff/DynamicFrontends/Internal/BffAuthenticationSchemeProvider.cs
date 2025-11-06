// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class BffAuthenticationSchemeProvider(
    IHttpContextAccessor httpContextAccessor,
    CurrentFrontendAccessor currentFrontendAccessor,
    IOptions<AuthenticationOptions> options,
    IOptions<BffOptions> bffOptions) : AuthenticationSchemeProvider(options)
{
    public override async Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync()
    {
        var defaultSchemes = await base.GetRequestHandlerSchemesAsync();

        if (httpContextAccessor.HttpContext != null
            && !currentFrontendAccessor.TryGet(out _)
            && bffOptions.Value.ConfigureOpenIdConnectDefaults != null)
        {
            defaultSchemes = defaultSchemes.Append(new BffAuthenticationScheme(
                BffAuthenticationSchemes.BffOpenIdConnect, "Default Duende Bff OpenIdConnect",
                typeof(OpenIdConnectHandler)));
        }

        return defaultSchemes;
    }

    public override async Task<AuthenticationScheme?> GetSchemeAsync(string name)
    {
        var scheme = await base.GetSchemeAsync(name);

        return scheme ?? GetBffAuthenticationScheme(name);
    }

    private BffAuthenticationScheme? GetBffAuthenticationScheme(string name)
    {
        BffFrontend? frontend = null;
        if (httpContextAccessor.HttpContext != null)
        {
            currentFrontendAccessor.TryGet(out frontend);
        }

        if (name == frontend?.CookieSchemeName || name == BffAuthenticationSchemes.BffCookie)
        {
            return new BffAuthenticationScheme(frontend?.CookieSchemeName ?? BffAuthenticationSchemes.BffCookie,
                "Duende Bff Cookie", typeof(CookieAuthenticationHandler));
        }

        if (name == frontend?.OidcSchemeName || name == BffAuthenticationSchemes.BffOpenIdConnect)
        {
            return new BffAuthenticationScheme(frontend?.OidcSchemeName ?? BffAuthenticationSchemes.BffOpenIdConnect,
                "Duende Bff OpenIdConnect", typeof(OpenIdConnectHandler));
        }

        return null;
    }

    private class BffAuthenticationScheme(Scheme name, string? displayName, Type handlerType)
        : AuthenticationScheme(name, displayName, handlerType);
}
