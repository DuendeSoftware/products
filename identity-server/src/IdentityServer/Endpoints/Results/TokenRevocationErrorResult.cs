// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Result for revocation error
/// </summary>
/// <seealso cref="IEndpointResult" />
public class TokenRevocationErrorResult : EndpointResult<TokenRevocationErrorResult>
{
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public string Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenRevocationErrorResult"/> class.
    /// </summary>
    /// <param name="error">The error.</param>
    public TokenRevocationErrorResult(string error)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }
}

internal class TokenRevocationErrorHttpWriter : IHttpResponseWriter<TokenRevocationErrorResult>
{
    public Task WriteHttpResponse(TokenRevocationErrorResult result, HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return context.Response.WriteJsonAsync(new { error = result.Error });
    }
}
