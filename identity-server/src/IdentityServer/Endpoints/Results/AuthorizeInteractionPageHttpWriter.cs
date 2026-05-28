// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Writes the HTTP response for authorize interaction page results (login, consent,
/// create-account, and custom redirect pages). This class can be subclassed to
/// customize redirect URL construction, add cookies or headers, or change the
/// response behavior. Register a subclass using
/// <c>AddHttpWriter&lt;AuthorizeInteractionPageResult, TWriter&gt;()</c>.
/// </summary>
public class AuthorizeInteractionPageHttpWriter : IHttpResponseWriter<AuthorizeInteractionPageResult>
{
    /// <summary>
    /// The IdentityServer options.
    /// </summary>
    protected IdentityServerOptions Options { get; }

    /// <summary>
    /// The server URL helper used to resolve base paths and origins.
    /// </summary>
    protected IServerUrls Urls { get; }

    /// <summary>
    /// The service used to persist UI locales across redirects.
    /// </summary>
    protected IUiLocalesService LocalesService { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeInteractionPageHttpWriter"/> class.
    /// </summary>
    /// <param name="options">The IdentityServer options.</param>
    /// <param name="urls">The server URL helper.</param>
    /// <param name="localesService">The UI locales service.</param>
    public AuthorizeInteractionPageHttpWriter(
        IdentityServerOptions options,
        IServerUrls urls,
        IUiLocalesService localesService)
    {
        Options = options;
        Urls = urls;
        LocalesService = localesService;
    }

    /// <inheritdoc/>
    public virtual async Task WriteHttpResponse(AuthorizeInteractionPageResult result, HttpContext context)
    {
        var returnUrl = await BuildReturnUrlAsync(result, context);
        var redirectUrl = await BuildRedirectUrlAsync(result, returnUrl, context);
        await WriteResponseAsync(context, redirectUrl);
    }

    /// <summary>
    /// Builds the return URL that will be passed as a query parameter to the
    /// interaction page. The return URL points back to the authorize callback
    /// endpoint and includes the original authorization parameters.
    /// </summary>
    /// <param name="result">The interaction page result.</param>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The return URL string.</returns>
    protected virtual Task<string> BuildReturnUrlAsync(AuthorizeInteractionPageResult result, HttpContext context)
    {
        var returnUrl = Urls.BasePath.EnsureTrailingSlash() + ProtocolRoutePaths.AuthorizeCallback;

        if (result.Request.PushedAuthorizationReferenceValue != null)
        {
            var requestUri = $"{PushedAuthorizationRequestUri}:{result.Request.PushedAuthorizationReferenceValue}";
            returnUrl = returnUrl
                .AddQueryString(OidcConstants.AuthorizeRequest.RequestUri, requestUri)
                .AddQueryString(OidcConstants.AuthorizeRequest.ClientId, result.Request.ClientId);
            var processedPrompt = result.Request.Raw[Constants.ProcessedPrompt];
            if (processedPrompt != null)
            {
                returnUrl = returnUrl.AddQueryString(Constants.ProcessedPrompt, processedPrompt);
            }
            var processedMaxAge = result.Request.Raw[Constants.ProcessedMaxAge];
            if (processedMaxAge != null)
            {
                returnUrl = returnUrl.AddQueryString(Constants.ProcessedMaxAge, processedMaxAge);
            }
        }
        else
        {
            returnUrl = returnUrl.AddQueryString(result.Request.ToOptimizedQueryString());
        }

        return Task.FromResult(returnUrl);
    }

    /// <summary>
    /// Builds the final redirect URL by combining the interaction page URL with
    /// the return URL as a query parameter. Handles conversion of the return URL
    /// to absolute form when redirecting to an external server, and persists UI
    /// locales for local redirects. The returned URL may be relative; it is
    /// resolved to an absolute URL when written to the response in
    /// <see cref="WriteResponseAsync"/>.
    /// </summary>
    /// <param name="result">The interaction page result.</param>
    /// <param name="returnUrl">The return URL built by <see cref="BuildReturnUrlAsync"/>.</param>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The redirect URL (may be relative; resolved to absolute in <see cref="WriteResponseAsync"/>).</returns>
    protected virtual async Task<string> BuildRedirectUrlAsync(AuthorizeInteractionPageResult result, string returnUrl, HttpContext context)
    {
        var url = result.RedirectUrl;
        if (!url.IsLocalUrl())
        {
            // this converts the relative redirect path to an absolute one if we're
            // redirecting to a different server
            returnUrl = Urls.Origin + returnUrl;
        }
        else
        {
            // if we're redirecting to a local URL, ensure we persist the UI locales
            // in a way .NET's localization will pick them up
            await LocalesService.StoreUiLocalesForRedirectAsync(result.Request.UiLocales, context.RequestAborted);
        }

        return url.AddQueryString(result.ReturnUrlParameterName, returnUrl);
    }

    /// <summary>
    /// Writes the HTTP redirect response. Override this method to add custom
    /// cookies, headers, or change the status code.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="redirectUrl">The fully constructed redirect URL.</param>
    protected virtual Task WriteResponseAsync(HttpContext context, string redirectUrl)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = Urls.GetAbsoluteUrl(redirectUrl);
        return Task.CompletedTask;
    }
}
