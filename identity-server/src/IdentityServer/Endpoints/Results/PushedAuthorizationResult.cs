// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Represents a successful result from the pushed authorization endpoint that can be written to the http response.
/// </summary>
public class PushedAuthorizationResult : EndpointResult<PushedAuthorizationResult>
{
    /// <summary>
    /// The successful response model.
    /// </summary>
    public PushedAuthorizationSuccess Response { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PushedAuthorizationResult"/> class.
    /// </summary>
    /// <param name="response">The successful response model.</param>
    public PushedAuthorizationResult(PushedAuthorizationSuccess response) => Response = response ?? throw new ArgumentNullException(nameof(response));
}

internal class PushedAuthorizationHttpWriter : IHttpResponseWriter<PushedAuthorizationResult>
{
    public async Task WriteHttpResponse(PushedAuthorizationResult result, HttpContext context)
    {
        context.Response.SetNoCache();
        context.Response.StatusCode = (int)HttpStatusCode.Created;
        var dto = new ResultDto
        {
            request_uri = result.Response.RequestUri,
            expires_in = result.Response.ExpiresIn
        };
        await context.Response.WriteJsonAsync(dto);
    }

    internal class ResultDto
    {
        public required string request_uri { get; set; }
        public required int expires_in { get; set; }
    }
}
