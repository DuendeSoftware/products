// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

#pragma warning disable 1591

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

public class Endpoint
{
    public Endpoint(string name, string path, Type handlerType)
    {
        Name = name;
        Path = path;
        Handler = handlerType;
    }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>
    /// The name.
    /// </value>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    /// <value>
    /// The path.
    /// </value>
    public PathString Path { get; set; }

    /// <summary>
    /// Gets or sets the handler.
    /// </summary>
    /// <value>
    /// The handler.
    /// </value>
    public Type Handler { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this endpoint uses a route template.
    /// This is used to determine if the endpoint needs to be parsed and matched.
    /// </summary>
    /// <value>
    /// If true, the endpoint uses a route template; otherwise, it does not.
    /// </value>
    public bool UsesRouteTemplate { get; set; }

    /// <summary>
    /// Function called on endpoint route matched to allow custom logic based on matched route values.
    /// </summary>
    /// <value>
    /// Function called on endpoint route matched to allow custom logic based on matched route values.
    /// </value>
    public Func<HttpContext, RouteValueDictionary, ILogger, bool>? OnRouteMatched { get; set; }
}
