// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Cookie-based session implementation
/// </summary>
/// <seealso cref="IUserSession" />
public class DefaultUserSession : IUserSession
{
    /// <summary>
    /// The HTTP context accessor
    /// </summary>
    protected readonly IHttpContextAccessor HttpContextAccessor;

    /// <summary>
    /// The handlers
    /// </summary>
    protected readonly IAuthenticationHandlerProvider Handlers;

    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The time provider
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// The server URL service.
    /// </summary>
    protected readonly IServerUrls Urls;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Gets the HTTP context.
    /// </summary>
    /// <value>
    /// The HTTP context.
    /// </value>
    protected HttpContext HttpContext => HttpContextAccessor.HttpContext;

    /// <summary>
    /// Gets the name of the check session cookie.
    /// </summary>
    /// <value>
    /// The name of the check session cookie.
    /// </value>
    protected string CheckSessionCookieName => Options.Authentication.CheckSessionCookieName;

    /// <summary>
    /// Gets the domain of the check session cookie.
    /// </summary>
    /// <value>
    /// The domain of the check session cookie.
    /// </value>
    protected string CheckSessionCookieDomain => Options.Authentication.CheckSessionCookieDomain;

    /// <summary>
    /// Gets the SameSite mode of the check session cookie.
    /// </summary>
    /// <value>
    /// The SameSite mode of the check session cookie.
    /// </value>
    protected SameSiteMode CheckSessionCookieSameSiteMode => Options.Authentication.CheckSessionCookieSameSiteMode;

    /// <summary>
    /// The principal
    /// </summary>
    protected ClaimsPrincipal Principal;

    /// <summary>
    /// The properties
    /// </summary>
    protected AuthenticationProperties Properties;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserSession"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="options">The options.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="urls"></param>
    /// <param name="logger">The logger.</param>
    public DefaultUserSession(
        IHttpContextAccessor httpContextAccessor,
        IAuthenticationHandlerProvider handlers,
        IdentityServerOptions options,
        TimeProvider timeProvider,
        IServerUrls urls,
        ILogger<IUserSession> logger)
    {
        HttpContextAccessor = httpContextAccessor;
        Handlers = handlers;
        Options = options;
        TimeProvider = timeProvider;
        Urls = urls;
        Logger = logger;
    }

    // we need this helper (and can't call HttpContext.AuthenticateAsync) so we don't run
    // claims transformation when we get the principal. this also ensures that we don't
    // re-issue a cookie that includes the claims from claims transformation.
    //
    // also, by caching the _principal/_properties it allows someone to issue a new
    // cookie (via HttpContext.SignInAsync) and we'll use those new values, rather than
    // just reading the incoming cookie
    //
    // this design requires this to be in DI as scoped

    /// <summary>
    /// Authenticates the authentication cookie for the current HTTP request and caches the user and properties results.
    /// </summary>
    protected virtual async Task AuthenticateAsync()
    {
        if (Principal == null || Properties == null)
        {
            var scheme = await HttpContext.GetCookieAuthenticationSchemeAsync();

            var handler = await Handlers.GetHandlerAsync(HttpContext, scheme);
            if (handler == null)
            {
                throw new InvalidOperationException($"No authentication handler is configured to authenticate for the scheme: {scheme}");
            }

            var result = await handler.AuthenticateAsync();
            if (result != null && result.Succeeded && result.Principal.Identity.IsAuthenticated)
            {
                Principal = result.Principal;
                Properties = result.Properties;
            }
        }
    }

    /// <summary>
    /// Creates a session identifier for the signin context and issues the session id cookie.
    /// </summary>
    /// <param name="principal"></param>
    /// <param name="properties"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    /// principal
    /// or
    /// properties
    /// </exception>
    public virtual async Task<string> CreateSessionIdAsync(ClaimsPrincipal principal, AuthenticationProperties properties, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(properties);

        var currentSubjectId = (await GetUserAsync(ct))?.GetSubjectId();
        var newSubjectId = principal.GetSubjectId();

        if (properties.GetSessionId() == null)
        {
            var currSid = await GetSessionIdAsync(ct);
            if (newSubjectId == currentSubjectId && currSid != null)
            {
                properties.SetSessionId(currSid);
                var clients = Properties.GetClientList();
                if (clients.Any())
                {
                    properties.SetClientList(clients);
                }
            }
            else
            {
                properties.SetSessionId(CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex));
            }
        }

        var sid = properties.GetSessionId();
        IssueSessionIdCookie(sid);

        Principal = principal;
        Properties = properties;

        return sid;
    }

    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<ClaimsPrincipal> GetUserAsync(Ct ct)
    {
        await AuthenticateAsync();

        return Principal;
    }

    /// <summary>
    /// Gets the current session identifier.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<string> GetSessionIdAsync(Ct ct)
    {
        await AuthenticateAsync();

        return Properties?.GetSessionId();
    }

    /// <summary>
    /// Ensures the session identifier cookie is synchronized with the current
    /// session identifier. If there is no sid, the cookie is removed. If there
    /// is a sid, and the session identifier cookie is missing, it is issued.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task EnsureSessionIdCookieAsync(Ct ct)
    {
        var sid = await GetSessionIdAsync(ct);
        if (sid != null)
        {
            IssueSessionIdCookie(sid);
        }
        else
        {
            await RemoveSessionIdCookieAsync(ct);
        }
    }

    /// <summary>
    /// Removes the session identifier cookie.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual Task RemoveSessionIdCookieAsync(Ct ct)
    {
        if (HttpContext.Request.Cookies.ContainsKey(CheckSessionCookieName))
        {
            // only remove it if we have it in the request
            var options = CreateSessionIdCookieOptions();
            options.Expires = TimeProvider.GetUtcNow().UtcDateTime.AddYears(-1);

            HttpContext.Response.Cookies.Append(CheckSessionCookieName, ".", options);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates the options for the session cookie.
    /// </summary>
    public virtual CookieOptions CreateSessionIdCookieOptions()
    {
        var secure = HttpContext.Request.IsHttps;
        var path = Urls.BasePath.CleanUrlPath();

        var options = new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            Path = path,
            IsEssential = true,
            Domain = CheckSessionCookieDomain,
            SameSite = CheckSessionCookieSameSiteMode
        };

        return options;
    }

    /// <summary>
    /// Issues the cookie that contains the session id.
    /// </summary>
    /// <param name="sid"></param>
    public virtual void IssueSessionIdCookie(string sid)
    {
        if (Options.Endpoints.EnableCheckSessionEndpoint)
        {
            if (HttpContext.Request.Cookies[CheckSessionCookieName] != sid)
            {
                HttpContext.Response.Cookies.Append(
                    Options.Authentication.CheckSessionCookieName,
                    sid,
                    CreateSessionIdCookieOptions());
            }
        }
    }

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">clientId</exception>
    public virtual async Task AddClientIdAsync(string clientId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        await AuthenticateAsync();
        if (Properties != null)
        {
            var clientIds = Properties.GetClientList();
            if (!clientIds.Contains(clientId))
            {
                Properties.AddClientId(clientId);
                await UpdateSessionCookie();
            }
        }
    }

    /// <summary>
    /// Gets the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<IEnumerable<string>> GetClientListAsync(Ct ct)
    {
        await AuthenticateAsync();

        if (Properties != null)
        {
            try
            {
                return Properties.GetClientList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error decoding client list");
                // clear so we don't keep failing
                Properties.RemoveClientList();
                await UpdateSessionCookie();
            }
        }

        return Enumerable.Empty<string>();
    }

    // client list helpers
    private async Task UpdateSessionCookie()
    {
        await AuthenticateAsync();

        if (Principal == null || Properties == null)
        {
            throw new InvalidOperationException("User is not currently authenticated");
        }

        var scheme = await HttpContext.GetCookieAuthenticationSchemeAsync();
        await HttpContext.SignInAsync(scheme, Principal, Properties);
    }

    /// <inheritdoc/>
    public virtual async Task AddSamlSessionAsync(SamlSpSessionData session)
    {
        ArgumentNullException.ThrowIfNull(session);

        await AuthenticateAsync();
        if (Properties != null)
        {
            Properties.AddSamlSession(session);
            await UpdateSessionCookie();
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<SamlSpSessionData>> GetSamlSessionListAsync()
    {
        await AuthenticateAsync();

        if (Properties != null)
        {
            try
            {
                return Properties.GetSamlSessionList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting SAML session list");
            }
        }

        return Array.Empty<SamlSpSessionData>();
    }

    /// <inheritdoc/>
    public virtual async Task RemoveSamlSessionAsync(string entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);

        await AuthenticateAsync();
        if (Properties != null)
        {
            Properties.RemoveSamlSession(entityId);
            await UpdateSessionCookie();
        }
    }
}
