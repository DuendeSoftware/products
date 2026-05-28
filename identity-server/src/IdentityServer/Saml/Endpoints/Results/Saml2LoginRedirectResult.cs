// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Saml.Endpoints.Results;

/// <summary>
/// Endpoint result that issues a redirect to the login page.
/// </summary>
/// <remarks>
/// This type exists separately from <see cref="Saml2LoginPageResult"/> because that type
/// bundles state persistence with login redirection. This type is used by
/// <c>SingleSignOnCallbackEndpoint</c> where state was already persisted on the initial request.
/// </remarks>
public class Saml2LoginRedirectResult(string redirectUrl) : EndpointResult<Saml2LoginRedirectResult>
{
    /// <summary>
    /// The URL to redirect to.
    /// </summary>
    public string RedirectUrl { get; } = redirectUrl ?? throw new ArgumentNullException(nameof(redirectUrl));
}

/// <summary>
/// Response writer for <see cref="Saml2LoginRedirectResult"/> that issues a 302 redirect.
/// </summary>
internal class Saml2LoginRedirectResultHttpWriter : IHttpResponseWriter<Saml2LoginRedirectResult>
{
    /// <inheritdoc/>
    public Task WriteHttpResponse(Saml2LoginRedirectResult result, HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = result.RedirectUrl;
        return Task.CompletedTask;
    }
}
