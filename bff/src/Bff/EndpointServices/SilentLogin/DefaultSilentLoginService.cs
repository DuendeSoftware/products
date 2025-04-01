// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Service for handling silent login requests
/// </summary>
[Obsolete("This endpoint will be removed in a future version. Use /login?prompt=create")]
public class DefaultSilentLoginService : ISilentLoginService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public DefaultSilentLoginService(IOptions<BffOptions> options, ILogger<DefaultSilentLoginService> logger)
    {
        Options = options.Value;
        Logger = logger;
    }

    /// <inheritdoc />
    public virtual Task ProcessRequestAsync(HttpContext context)
    {
        var query = context.Request.QueryString;
        if (!string.IsNullOrEmpty(query.Value))
        {
            query = query.Add(Constants.RequestParameters.Prompt, "none");
        }

        context.Response.Redirect($"{Options.LoginPath}?{query}");
        return Task.CompletedTask;
    }
}
