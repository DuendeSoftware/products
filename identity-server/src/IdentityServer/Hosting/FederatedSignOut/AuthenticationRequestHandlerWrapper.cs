// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.FederatedSignOut;

internal class AuthenticationRequestHandlerWrapper : IAuthenticationRequestHandler
{
    private const string IframeHtml = "<iframe style='display:none' width='0' height='0' src='{0}'></iframe>";

    private readonly IAuthenticationRequestHandler _inner;
    private readonly HttpContext _context;
    private readonly ILogger _logger;

    public AuthenticationRequestHandlerWrapper(IAuthenticationRequestHandler inner, IHttpContextAccessor httpContextAccessor)
    {
        _inner = inner;
        _context = httpContextAccessor.HttpContext;

        var factory = _context.RequestServices.GetService<ILoggerFactory>();
        _logger = factory?.CreateLogger(GetType());
    }

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => _inner.InitializeAsync(scheme, context);

    public async Task<bool> HandleRequestAsync()
    {
        var result = await _inner.HandleRequestAsync();

        if (result && _context.GetSignOutCalled() && _context.Response.StatusCode == 200)
        {
            // given that this runs prior to the authentication middleware running
            // we need to explicitly trigger authentication so we can have our 
            // session service populated with the current user info
            await _context.AuthenticateAsync();

            // now we can do our processing to render the iframe (if needed)
            await ProcessFederatedSignOutRequestAsync();
        }

        return result;
    }

    public Task<AuthenticateResult> AuthenticateAsync() => _inner.AuthenticateAsync();

    public Task ChallengeAsync(AuthenticationProperties properties) => _inner.ChallengeAsync(properties);

    public Task ForbidAsync(AuthenticationProperties properties) => _inner.ForbidAsync(properties);

    private async Task ProcessFederatedSignOutRequestAsync()
    {
        _logger?.LogDebug("Processing federated signout");

        var iframeUrl = await _context.GetIdentityServerSignoutFrameCallbackUrlAsync();
        if (iframeUrl != null)
        {
            _logger?.LogDebug("Rendering signout callback iframe");
            await RenderResponseAsync(iframeUrl);
        }
        else
        {
            _logger?.LogDebug("No signout callback iframe to render");
        }
    }

    private async Task RenderResponseAsync(string iframeUrl)
    {
        _context.Response.SetNoCache();

        if (_context.Response.Body.CanWrite)
        {
            var iframe = string.Format(IframeHtml, iframeUrl);
            _context.Response.ContentType = "text/html";
            await _context.Response.WriteAsync(iframe);
            await _context.Response.Body.FlushAsync();
        }
    }
}
