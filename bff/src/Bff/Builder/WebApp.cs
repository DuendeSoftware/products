// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Duende.Bff.Builder;

/// <summary>
/// Wrapper around the ASP.NET Core application builder and endpoint route builder.
///
/// Similar to <see cref="WebApplication"/>. Since aspnet core doesn't allow you access to this,
/// but this is the most friendly way to configure a BFF application, we provide this wrapper.
///
/// Use it to register custom middlewares or configure endpoints. 
/// 
/// </summary>
public sealed class WebApp : IApplicationBuilder, IEndpointRouteBuilder
{
    private readonly IApplicationBuilder _appBuilder;
    private readonly IEndpointRouteBuilder _endpointRouteBuilder;

    /// <summary>
    /// Internal constructor to prevent creation of this class outside of the BFF library.
    /// </summary>
    /// <param name="appBuilder"></param>
    /// <param name="endpointRouteBuilder"></param>
    internal WebApp(IApplicationBuilder appBuilder, IEndpointRouteBuilder endpointRouteBuilder)
    {
        _appBuilder = appBuilder;
        _endpointRouteBuilder = endpointRouteBuilder;
    }

    public IApplicationBuilder CreateApplicationBuilder() => _endpointRouteBuilder.CreateApplicationBuilder();

    public IServiceProvider ServiceProvider => _endpointRouteBuilder.ServiceProvider;

    public ICollection<EndpointDataSource> DataSources => _endpointRouteBuilder.DataSources;
    public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => _appBuilder.Use(middleware);

    public IApplicationBuilder New() => _appBuilder.New();

    public RequestDelegate Build() => _appBuilder.Build();

    public IServiceProvider ApplicationServices
    {
        get => _appBuilder.ApplicationServices;
        set => _appBuilder.ApplicationServices = value;
    }

    public IFeatureCollection ServerFeatures => _appBuilder.ServerFeatures;

    public IDictionary<string, object?> Properties => _appBuilder.Properties;

    [Obsolete("Don't call UseEndpoints on WebApp. The WebApp directly implements IEndpointRouteBuilder.")]
    public WebApp UseEndpoints(Action<IEndpointRouteBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_endpointRouteBuilder);
        return this;
    }
}
