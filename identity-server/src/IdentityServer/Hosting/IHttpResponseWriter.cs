// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Duende.IdentityServer.Hosting;

/// <summary>
/// Contract for a service that writes appropriate http responses for <see
/// cref="IEndpointResult"/> objects.
/// </summary>
public interface IHttpResponseWriter<in T>
    where T : IEndpointResult
{
    /// <summary>
    /// Writes the endpoint result to the HTTP response.
    /// </summary>
    Task WriteHttpResponse(T result, HttpContext context);
}

