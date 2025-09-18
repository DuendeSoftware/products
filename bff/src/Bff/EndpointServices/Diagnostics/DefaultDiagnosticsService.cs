// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Default debug diagnostics service
/// </summary>
public class DefaultDiagnosticsService : IDiagnosticsService
{
    /// <summary>
    /// The environment
    /// </summary>
    protected readonly IWebHostEnvironment Environment;

    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly IOptions<BffOptions> Options;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="environment"></param>
    /// <param name="options"></param>
    public DefaultDiagnosticsService(IWebHostEnvironment environment, IOptions<BffOptions> options)
    {
        Environment = environment;
        Options = options;
    }

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        if (Options.Value.DiagnosticsEnvironments?.Contains(Environment.EnvironmentName) is null or false)
        {
            context.Response.StatusCode = 404;
            return;
        }

        var usertoken = await context.GetUserAccessTokenAsync();
        var clientToken = await context.GetClientAccessTokenAsync();

        var info = new DiagnosticsInfo
        {
            UserAccessToken = usertoken.AccessToken,
            ClientAccessToken = clientToken.AccessToken
        };

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(info, options);

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(json);
    }

    private class DiagnosticsInfo
    {
        public string? UserAccessToken { get; set; }
        public string? ClientAccessToken { get; set; }
    }
}
