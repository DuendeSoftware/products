// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.Bff;

public sealed class BffApplication : IHost, IApplicationBuilder, IEndpointRouteBuilder, IAsyncDisposable
{
    private readonly WebApplication _webApplication;

    internal BffApplication(WebApplication webApplication)
    {
        _webApplication = webApplication;
        _webApplication.UseHttpsRedirection();
        _webApplication.UseRouting();

        _webApplication.UseBff();
    }

    public IFrontendCollection Frontends => _webApplication.Services.GetRequiredService<IFrontendCollection>();

    public void Dispose() => ((IDisposable)_webApplication).Dispose();

    public async Task StartAsync(CT ct = new CT()) => await _webApplication.StartAsync(ct);

    public async Task StopAsync(CT ct = new CT()) => await _webApplication.StopAsync(ct);

    public IServiceProvider Services => _webApplication.Services;

    public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => _webApplication.Use(middleware);

    public IApplicationBuilder New() => ((IApplicationBuilder)_webApplication).New();

    public RequestDelegate Build() => ((IApplicationBuilder)_webApplication).Build();

    public IServiceProvider ApplicationServices
    {
        get => ((IApplicationBuilder)_webApplication).ApplicationServices;
        set => ((IApplicationBuilder)_webApplication).ApplicationServices = value;
    }

    public IFeatureCollection ServerFeatures => ((IApplicationBuilder)_webApplication).ServerFeatures;

    public IDictionary<string, object?> Properties => ((IApplicationBuilder)_webApplication).Properties;

    public IApplicationBuilder CreateApplicationBuilder() => ((IEndpointRouteBuilder)_webApplication).CreateApplicationBuilder();

    public IServiceProvider ServiceProvider => ((IEndpointRouteBuilder)_webApplication).ServiceProvider;

    public ICollection<EndpointDataSource> DataSources => ((IEndpointRouteBuilder)_webApplication).DataSources;

    public async ValueTask DisposeAsync() => await _webApplication.DisposeAsync();
}
