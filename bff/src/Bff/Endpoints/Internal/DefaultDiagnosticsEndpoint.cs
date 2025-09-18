// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Default debug diagnostics service
/// </summary>
internal class DefaultDiagnosticsEndpoint(IWebHostEnvironment environment, IOptions<BffOptions> options)
    : IDiagnosticsEndpoint
{

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CT ct = default)
    {
        if (options.Value.DiagnosticsEnvironments?.Contains(environment.EnvironmentName) is null or false)
        {
            context.Response.StatusCode = 404;
            return;
        }

        var tokenResult = await context.GetUserAccessTokenAsync(null, ct);
        var clientTokenResult = await context.GetClientAccessTokenAsync(null, ct);

        if (!tokenResult.WasSuccessful(out var userToken, out var failure)
            || !clientTokenResult.WasSuccessful(out var clientToken, out failure))
        {
            var error = new
            {
                failure.Error,
                failure.ErrorDescription
            };
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(error), ct);
            return;
        }

        var info = new
        {
            UserAccessToken = userToken.AccessToken,
            ClientAccessToken = clientToken.AccessToken
        };

        var json = JsonSerializer.Serialize(info, JsonSerializerOptions);

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(json, ct);
    }
}
