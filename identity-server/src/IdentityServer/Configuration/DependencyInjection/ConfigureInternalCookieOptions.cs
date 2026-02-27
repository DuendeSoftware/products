// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Stores.Default;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Configuration;

internal class ConfigureInternalCookieOptions : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    private readonly IdentityServerOptions _idsrv;

    public ConfigureInternalCookieOptions(IdentityServerOptions idsrv) => _idsrv = idsrv;

    public void Configure(CookieAuthenticationOptions options)
    {
    }

    public void Configure(string name, CookieAuthenticationOptions options)
    {
        if (name == IdentityServerConstants.DefaultCookieAuthenticationScheme)
        {
            options.SlidingExpiration = _idsrv.Authentication.CookieSlidingExpiration;
            options.ExpireTimeSpan = _idsrv.Authentication.CookieLifetime;
            options.Cookie.Name = IdentityServerConstants.DefaultCookieAuthenticationScheme;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = _idsrv.Authentication.CookieSameSiteMode;

            options.LoginPath = ExtractLocalUrl(_idsrv.UserInteraction.LoginUrl);
            options.LogoutPath = ExtractLocalUrl(_idsrv.UserInteraction.LogoutUrl);
            if (_idsrv.UserInteraction.LoginReturnUrlParameter != null)
            {
                options.ReturnUrlParameter = _idsrv.UserInteraction.LoginReturnUrlParameter;
            }

            options.Events.OnCheckSlidingExpiration = CreateCheckSlidingExpirationCallback(options.Events.OnCheckSlidingExpiration);
        }

        if (name == IdentityServerConstants.ExternalCookieAuthenticationScheme)
        {
            options.Cookie.Name = IdentityServerConstants.ExternalCookieAuthenticationScheme;
            options.Cookie.IsEssential = true;
            // https://github.com/IdentityServer/IdentityServer4/issues/2595
            // need to set None because iOS 12 safari considers the POST back to the client from the
            // IdP as not safe, so cookies issued from response (with lax) then should not be honored.
            // so we need to make those cookies issued without same-site, thus the browser will
            // hold onto them and send on the next redirect to the callback page.
            // see: https://brockallen.com/2019/01/11/same-site-cookies-asp-net-core-and-external-authentication-providers/
            options.Cookie.SameSite = _idsrv.Authentication.CookieSameSiteMode;
        }
    }

    private static string ExtractLocalUrl(string url)
    {
        if (url.IsLocalUrl())
        {
            if (url.StartsWith("~/", StringComparison.Ordinal))
            {
                url = url.Substring(1);
            }

            return url;
        }

        return null;
    }

    private Func<CookieSlidingExpirationContext, Task> CreateCheckSlidingExpirationCallback(
        Func<CookieSlidingExpirationContext, Task> inner)
    {
        return Callback;

        async Task Callback(CookieSlidingExpirationContext context)
        {
            await ServerSideSessionCookieEvents.OnCheckSlidingExpiration(context);
            await inner?.Invoke(context)!;
        }
    }
}

internal class PostConfigureInternalCookieOptions : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly IdentityServerOptions _idsrv;
    private readonly IOptions<Microsoft.AspNetCore.Authentication.AuthenticationOptions> _authOptions;
    private readonly ILogger _logger;

    public PostConfigureInternalCookieOptions(
        IdentityServerOptions idsrv,
        IOptions<Microsoft.AspNetCore.Authentication.AuthenticationOptions> authOptions,
        ILoggerFactory loggerFactory)
    {
        _idsrv = idsrv;
        _authOptions = authOptions;
        _logger = loggerFactory.CreateLogger("Duende.IdentityServer.Startup");
    }

    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        var scheme = _idsrv.Authentication.CookieAuthenticationScheme ??
                     _authOptions.Value.DefaultAuthenticateScheme ??
                     _authOptions.Value.DefaultScheme;

        if (name == scheme)
        {
            _idsrv.UserInteraction.LoginUrl = _idsrv.UserInteraction.LoginUrl ?? options.LoginPath;
            _idsrv.UserInteraction.LoginReturnUrlParameter = _idsrv.UserInteraction.LoginReturnUrlParameter ?? options.ReturnUrlParameter;
            _idsrv.UserInteraction.LogoutUrl = _idsrv.UserInteraction.LogoutUrl ?? options.LogoutPath;

            _logger.LogDebug("Login Url: {url}", _idsrv.UserInteraction.LoginUrl);
            _logger.LogDebug("Login Return Url Parameter: {param}", _idsrv.UserInteraction.LoginReturnUrlParameter);
            _logger.LogDebug("Logout Url: {url}", _idsrv.UserInteraction.LogoutUrl);

            _logger.LogDebug("ConsentUrl Url: {url}", _idsrv.UserInteraction.ConsentUrl);
            _logger.LogDebug("Consent Return Url Parameter: {param}", _idsrv.UserInteraction.ConsentReturnUrlParameter);

            _logger.LogDebug("Error Url: {url}", _idsrv.UserInteraction.ErrorUrl);
            _logger.LogDebug("Error Id Parameter: {param}", _idsrv.UserInteraction.ErrorIdParameter);
        }
    }
}
