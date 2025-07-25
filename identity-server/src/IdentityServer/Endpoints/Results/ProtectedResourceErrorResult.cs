// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Models result of a protected resource
/// </summary>
public class ProtectedResourceErrorResult : EndpointResult<ProtectedResourceErrorResult>
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
    public ProtectedResourceErrorResult(string error, string errorDescription = null)
    {
        Error = error;
        ErrorDescription = errorDescription;
    }
}

internal class ProtectedResourceErrorHttpWriter : IHttpResponseWriter<ProtectedResourceErrorResult>
{
    public Task WriteHttpResponse(ProtectedResourceErrorResult result, HttpContext context)
    {
        context.Response.StatusCode = 401;
        context.Response.SetNoCache();

        var error = result.Error;
        var errorDescription = result.ErrorDescription;

        if (Constants.ProtectedResourceErrorStatusCodes.TryGetValue(error, out var code))
        {
            context.Response.StatusCode = code;
        }

        if (error == OidcConstants.ProtectedResourceErrors.ExpiredToken)
        {
            error = OidcConstants.ProtectedResourceErrors.InvalidToken;
            errorDescription = "The access token expired";
        }

        var values = new List<string>
        {
            """
            Bearer realm="IdentityServer"
            """,
            $"""
             error="{error}"
             """
        };

        if (!errorDescription.IsMissing())
        {
            values.Add($"""
                        error_description="{errorDescription}"
                        """);
        }

        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, string.Join(",", values));

        return Task.CompletedTask;
    }
}
