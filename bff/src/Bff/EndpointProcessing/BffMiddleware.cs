// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints;

/// <summary>
/// Middleware to provide anti-forgery protection via a static header and 302 to 401 conversion
/// Must run *before* the authorization middleware
/// </summary>
public class BffMiddleware
{
    private readonly RequestDelegate _next;
    private readonly BffOptions _options;
    private readonly ILogger<BffMiddleware> _logger;
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="next"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    /// <param name="authenticationSchemeProvider"></param>
    public BffMiddleware(RequestDelegate next, IOptions<BffOptions> options, ILogger<BffMiddleware> logger, IAuthenticationSchemeProvider authenticationSchemeProvider)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
        _authenticationSchemeProvider = authenticationSchemeProvider;
    }

    /// <summary>
    /// Request processing
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task Invoke(HttpContext context)
    {
        // add marker so we can determine if middleware has run later in the pipeline
        context.Items[Constants.BffMiddlewareMarker] = true;

        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        var isBffEndpoint = endpoint.Metadata.GetMetadata<IBffApiEndpoint>() != null;
        if (isBffEndpoint)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userTokens = context.RequestServices.GetRequiredService<IUserTokenStore>();
                var token = await userTokens.GetTokenAsync(context.User);

                if (token.Expiration < DateTimeOffset.Now)
                {
                    _logger.LogInformation("expired");

                    var tokenService = context.RequestServices.GetRequiredService<IUserTokenManagementService>();

                    token = await tokenService.GetAccessTokenAsync(context.User);

                    if (token.Expiration < DateTimeOffset.Now)
                    {
                        // get rid of local cookie first
                        var signInScheme = await _authenticationSchemeProvider.GetDefaultSignInSchemeAsync();
                        await context.SignOutAsync(signInScheme?.Name);

                        var props = new AuthenticationProperties
                        {
                            RedirectUri = "/"
                        };


                        // trigger idp logout
                        await context.SignOutAsync(props);
                        //context.RequestServices.GetRequiredService<IUserSessionStore>().DeleteUserSessionAsync()
                        context.Response.StatusCode = 401;
                        return;
                    }

                }
            }


            var requireAntiForgeryCheck = endpoint.Metadata.GetMetadata<IBffApiSkipAntiforgery>() == null;
            if (requireAntiForgeryCheck)
            {
                if (!context.CheckAntiForgeryHeader(_options))
                {
                    _logger.AntiForgeryValidationFailed(context.Request.Path);

                    context.Response.StatusCode = 401;
                    return;
                }
            }
        }
        
        var isUIEndpoint = endpoint.Metadata.GetMetadata<IBffUIApiEndpoint>() != null;
        if (isUIEndpoint && context.IsAjaxRequest())
        {
            _logger.LogDebug("BFF management endpoint {endpoint} is only intended for a browser window to request and load. It is not intended to be accessed with Ajax or fetch requests.", context.Request.Path);
        }

        await _next(context);
    }
}