// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.IdentityServer.Configuration.Models;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration.ResponseGeneration;

/// <inheritdoc/>
public class DynamicClientRegistrationResponseGenerator : IDynamicClientRegistrationResponseGenerator
{
    /// <summary>
    /// The options used for serializing json in responses.
    /// </summary>
    protected JsonSerializerOptions SerializerOptions { get; set; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<DynamicClientRegistrationResponseGenerator> Logger;

    /// <inheritdoc/>
    public DynamicClientRegistrationResponseGenerator(ILogger<DynamicClientRegistrationResponseGenerator> logger) => Logger = logger;

    /// <inheritdoc/>
    public virtual async Task WriteResponse<T>(HttpContext context, int statusCode, T response)
        where T : IDynamicClientRegistrationResponse
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(response, SerializerOptions);
    }

    /// <inheritdoc/>
    public virtual Task WriteContentTypeError(HttpContext context)
    {
        Logger.LogDebug("Invalid content type in dynamic client registration request");
        context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual async Task WriteBadRequestError(HttpContext context) =>
        await WriteResponse(context, StatusCodes.Status400BadRequest,
            new DynamicClientRegistrationError(
                DynamicClientRegistrationErrors.InvalidClientMetadata,
                "malformed metadata document")
        );

    /// <inheritdoc/>
    public virtual async Task WriteError(HttpContext context, DynamicClientRegistrationError error) =>
        await WriteResponse(context, StatusCodes.Status400BadRequest, error);


    /// <inheritdoc/>
    public virtual async Task WriteSuccessResponse(HttpContext context, DynamicClientRegistrationResponse response) =>
        await WriteResponse(context, StatusCodes.Status201Created, response);
}
