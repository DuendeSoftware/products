// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Result for an interactive page
/// </summary>
/// <seealso cref="IEndpointResult" />
public abstract class AuthorizeInteractionPageResult : EndpointResult<AuthorizeInteractionPageResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeInteractionPageResult"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="redirectUrl"></param>
    /// <param name="returnUrlParameterName"></param>
    /// <exception cref="System.ArgumentNullException">request</exception>
    public AuthorizeInteractionPageResult(ValidatedAuthorizeRequest request, string redirectUrl, string returnUrlParameterName)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        RedirectUrl = redirectUrl ?? throw new ArgumentNullException(nameof(redirectUrl));
        ReturnUrlParameterName = returnUrlParameterName ?? throw new ArgumentNullException(nameof(returnUrlParameterName));
    }

    /// <summary>
    /// The validated authorize request
    /// </summary>
    public ValidatedAuthorizeRequest Request { get; }

    /// <summary>
    /// The redirect URI
    /// </summary>
    public string RedirectUrl { get; }

    /// <summary>
    /// The return URL param name
    /// </summary>
    public string ReturnUrlParameterName { get; }
}

internal class AuthorizeInteractionPageHttpWriter : IHttpResponseWriter<AuthorizeInteractionPageResult>
{
    private readonly IServerUrls _urls;
    private readonly IAuthorizationParametersMessageStore _authorizationParametersMessageStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeInteractionPageResult"/> class.
    /// </summary>
    public AuthorizeInteractionPageHttpWriter(
        IServerUrls urls,
        IAuthorizationParametersMessageStore authorizationParametersMessageStore = null)
    {
        _urls = urls;
        _authorizationParametersMessageStore = authorizationParametersMessageStore;
    }

    /// <inheritdoc/>
    public async Task WriteHttpResponse(AuthorizeInteractionPageResult result, HttpContext context)
    {
        var returnUrl = _urls.BasePath.EnsureTrailingSlash() + ProtocolRoutePaths.AuthorizeCallback;

        // IAuthorizationParametersMessageStore is deprecated and will be removed someday
        if (_authorizationParametersMessageStore != null)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var msg = new Message<IDictionary<string, string[]>>(result.Request.ToOptimizedFullDictionary());
#pragma warning restore CS0618 // Type or member is obsolete
            var id = await _authorizationParametersMessageStore.WriteAsync(msg);
            returnUrl = returnUrl.AddQueryString(Constants.AuthorizationParamsStore.MessageStoreIdParameterName, id);
        }
        else
        {
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
        }

        var url = result.RedirectUrl;
        if (!url.IsLocalUrl())
        {
            // this converts the relative redirect path to an absolute one if we're 
            // redirecting to a different server
            returnUrl = _urls.Origin + returnUrl;
        }

        url = url.AddQueryString(result.ReturnUrlParameterName, returnUrl);
        context.Response.Redirect(_urls.GetAbsoluteUrl(url));
    }
}
