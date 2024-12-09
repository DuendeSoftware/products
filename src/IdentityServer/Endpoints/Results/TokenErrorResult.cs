// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Duende.IdentityServer.Extensions;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Models a token error result
/// </summary>
public class TokenErrorResult : EndpointResult<TokenErrorResult>
{
    /// <summary>
    /// The response
    /// </summary>
    public TokenErrorResponse Response { get; }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="error"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public TokenErrorResult(TokenErrorResponse error)
    {
        if (error.Error.IsMissing()) throw new ArgumentNullException(nameof(error.Error), "Error must be set");

        Response = error;
    }
}

internal class TokenErrorHttpWriter : IHttpResponseWriter<TokenErrorResult>
{
    public Task WriteHttpResponse(TokenErrorResult result, HttpContext context)
    {
        context.Response.StatusCode = 400;
        context.Response.SetNoCache();

        if (result.Response.DPoPNonce.IsPresent())
        {
            context.Response.Headers[OidcConstants.HttpHeaders.DPoPNonce] = result.Response.DPoPNonce;
        }

        var dto = new ResultDto
        {
            error = result.Response.Error,
            error_description = result.Response.ErrorDescription,

            custom = result.Response.Custom
        };

        return context.Response.WriteJsonAsync(dto);
    }

    internal class ResultDto
    {
        public string error { get; set; }
        public string error_description { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> custom { get; set; }
    }
}