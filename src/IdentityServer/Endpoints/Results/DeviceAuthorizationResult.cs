// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Threading.Tasks;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// The result of device authorization
/// </summary>
public class DeviceAuthorizationResult : EndpointResult<DeviceAuthorizationResult>
{
    /// <summary>
    /// The response
    /// </summary>
    public DeviceAuthorizationResponse Response { get; }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="response"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public DeviceAuthorizationResult(DeviceAuthorizationResponse response)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
    }
}

internal class DeviceAuthorizationHttpWriter : IHttpResponseWriter<DeviceAuthorizationResult>
{
    public Task WriteHttpResponse(DeviceAuthorizationResult result, HttpContext context)
    {
        context.Response.SetNoCache();

        var dto = new ResultDto
        {
            device_code = result.Response.DeviceCode,
            user_code = result.Response.UserCode,
            verification_uri = result.Response.VerificationUri,
            verification_uri_complete = result.Response.VerificationUriComplete,
            expires_in = result.Response.DeviceCodeLifetime,
            interval = result.Response.Interval
        };

        return context.Response.WriteJsonAsync(dto);
    }

    internal class ResultDto
    {
        public string device_code { get; set; }
        public string user_code { get; set; }
        public string verification_uri { get; set; }
        public string verification_uri_complete { get; set; }
        public int expires_in { get; set; }
        public int interval { get; set; }
    }
}