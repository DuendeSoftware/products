// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Threading.Tasks;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;
using Duende.IdentityServer.Extensions;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// The result of a bad request
/// </summary>
public class BadRequestResult : EndpointResult<BadRequestResult>
{
    /// <summary>
    /// The error
    /// </summary>
    public string Error { get; }
    /// <summary>
    /// The error description
    /// </summary>
    public string ErrorDescription { get; }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="error"></param>
    /// <param name="errorDescription"></param>
    public BadRequestResult(string error = null, string errorDescription = null)
    {
        Error = error;
        ErrorDescription = errorDescription;
    }
}

internal class BadRequestHttpWriter : IHttpResponseWriter<BadRequestResult>
{
    public Task WriteHttpResponse(BadRequestResult result, HttpContext context)
    {
        context.Response.StatusCode = 400;
        context.Response.SetNoCache();

        if (result.Error.IsPresent())
        {
            var dto = new ResultDto
            {
                error = result.Error,
                error_description = result.ErrorDescription
            };

            return context.Response.WriteJsonAsync(dto);
        }

        return Task.CompletedTask;
    }

    internal class ResultDto
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }
}