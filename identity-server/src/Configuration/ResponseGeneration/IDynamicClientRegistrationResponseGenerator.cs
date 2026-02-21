// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration.Models;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration.ResponseGeneration;

/// <summary>
/// Generates dynamic client registration responses.
/// </summary>
public interface IDynamicClientRegistrationResponseGenerator
{
    /// <summary>
    /// Writes a response object to the HTTP context with the given status code.
    /// </summary>
    /// <typeparam name="T">The type of the response object that implements the <see cref="IDynamicClientRegistrationResponse"/> interface.</typeparam>
    /// <param name="context">The HTTP context to write the response to.</param>
    /// <param name="statusCode">The status code to set in the response.</param>
    /// <param name="response">The response object to write to the response.</param>
    /// <param name="ct">The cancellation token.</param>
    Task WriteResponse<T>(HttpContext context, int statusCode, T response, CT ct)
        where T : IDynamicClientRegistrationResponse;

    /// <summary>
    /// Writes a content type error to the HTTP response.
    /// </summary>
    /// <param name="response">The HTTP context to write the error to.</param>
    /// <param name="ct">The cancellation token.</param>
    Task WriteContentTypeError(HttpContext response, CT ct);

    /// <summary>
    /// Writes a bad request error to the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context to write the error to.</param>
    /// <param name="ct">The cancellation token.</param>
    Task WriteBadRequestError(HttpContext context, CT ct);

    /// <summary>
    /// Writes a success response to the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context to write the response to.</param>
    /// <param name="response">The dynamic client registration response.</param>
    /// <param name="ct">The cancellation token.</param>
    Task WriteSuccessResponse(HttpContext context, DynamicClientRegistrationResponse response, CT ct);

    /// <summary>
    /// Writes a validation or processing step's error to the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context to write the error to.</param>
    /// <param name="error">The dynamic client registration validation error.</param>
    /// <param name="ct">The cancellation token.</param>
    Task WriteError(HttpContext context, DynamicClientRegistrationError error, CT ct);
}
