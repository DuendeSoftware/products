// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Result for introspection
/// </summary>
/// <seealso cref="IEndpointResult" />
public class IntrospectionResult : EndpointResult<IntrospectionResult>
{
    /// <summary>
    /// Gets the result.
    /// </summary>
    /// <value>
    /// The result.
    /// </value>
    public Dictionary<string, object> Entries { get; }

    /// <summary>
    /// Gets the name of the caller.
    /// </summary>
    /// <value>
    /// Identifier of the request caller.
    /// </value>
    public string CallerName { get; }

    /// <summary>
    /// Gets if JWT response was requested.
    /// </summary>
    /// <value>
    /// True if JWT response was requested.
    /// </value>
    public bool JwtResponseWasRequested { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntrospectionResult"/> class.
    /// </summary>
    /// <param name="entries">The result.</param>
    /// <exception cref="System.ArgumentNullException">result</exception>
    public IntrospectionResult(Dictionary<string, object> entries) : this(entries, null, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntrospectionResult"/> class.
    /// </summary>
    /// <param name="entries">The result.</param>
    /// <param name="callerName">The identifier of the party making the introspection request.</param>
    /// <param name="jwtResponseWasRequested">If a JWT response was requested.</param>
    /// <exception cref="System.ArgumentNullException">result</exception>
    public IntrospectionResult(Dictionary<string, object> entries, string callerName, bool jwtResponseWasRequested)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        CallerName = callerName;
        JwtResponseWasRequested = jwtResponseWasRequested;
    }
}

internal class IntrospectionHttpWriter(IIssuerNameService issuerNameService, ITokenCreationService tokenCreationService)
    : IHttpResponseWriter<IntrospectionResult>
{
    public async Task WriteHttpResponse(IntrospectionResult result, HttpContext context)
    {
        context.Response.SetNoCache();

        if (result.JwtResponseWasRequested)
        {
            context.Response.Headers.ContentType = $"application/{JwtClaimTypes.JwtTypes.IntrospectionJwtResponse}";
            var token = new Token
            {
                Type = JwtClaimTypes.JwtTypes.IntrospectionJwtResponse,
                Issuer = await issuerNameService.GetCurrentAsync(context.RequestAborted),
                Audiences = [result.CallerName],
                CreationTime = DateTime.UtcNow,
                Claims = [new Claim("token_introspection", ObjectSerializer.ToString(result.Entries), IdentityServerConstants.ClaimValueTypes.Json)]
            };
            var jwt = await tokenCreationService.CreateTokenAsync(token, context.RequestAborted);

            await context.Response.WriteAsync(jwt);
        }
        else
        {
            await context.Response.WriteJsonAsync(result.Entries);
        }
    }
}
