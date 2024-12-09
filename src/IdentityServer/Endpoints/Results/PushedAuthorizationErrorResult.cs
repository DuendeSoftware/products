// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Represents an error result from the pushed authorization endpoint that can be written to the http response.
/// </summary>
public class PushedAuthorizationErrorResult : EndpointResult<PushedAuthorizationErrorResult>
{
    
    /// <summary>
    /// The error response model.
    /// </summary>
    public PushedAuthorizationFailure Response { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PushedAuthorizationErrorResult"/> class.
    /// </summary>
    /// <param name="response">The error response model.</param>
    public PushedAuthorizationErrorResult(PushedAuthorizationFailure response)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
    }
}

internal class PushedAuthorizationErrorHttpWriter : IHttpResponseWriter<PushedAuthorizationErrorResult>
{
    public Task WriteHttpResponse(PushedAuthorizationErrorResult result, HttpContext context)
    {
        context.Response.SetNoCache();
        context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
        var dto = new ResultDto
        {
            error = result.Response.Error,
            error_description = result.Response.ErrorDescription,
        };

        return context.Response.WriteJsonAsync(dto);
    }

    internal class ResultDto
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }
}
