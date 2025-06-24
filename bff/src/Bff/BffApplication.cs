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
    private readonly WebApplication _app;

    internal BffApplication(WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseBff();
        _app = app;
    }

    public IFrontendCollection Frontends => _app.Services.GetRequiredService<IFrontendCollection>();

    public void Dispose() => ((IDisposable)_app).Dispose();

    public async Task StartAsync(CT ct = default) => await _app.StartAsync(ct);

    public async Task StopAsync(CT ct = default) => await _app.StopAsync(ct);

    public IServiceProvider Services => _app.Services;

    public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => _app.Use(middleware);

    public IApplicationBuilder New() => ((IApplicationBuilder)_app).New();

    public RequestDelegate Build() => ((IApplicationBuilder)_app).Build();

    public IServiceProvider ApplicationServices
    {
        get => ((IApplicationBuilder)_app).ApplicationServices;
        set => ((IApplicationBuilder)_app).ApplicationServices = value;
    }

    public IFeatureCollection ServerFeatures => ((IApplicationBuilder)_app).ServerFeatures;

    public IDictionary<string, object?> Properties => ((IApplicationBuilder)_app).Properties;

    public IApplicationBuilder CreateApplicationBuilder() => ((IEndpointRouteBuilder)_app).CreateApplicationBuilder();

    public IServiceProvider ServiceProvider => ((IEndpointRouteBuilder)_app).ServiceProvider;

    public ICollection<EndpointDataSource> DataSources => ((IEndpointRouteBuilder)_app).DataSources;

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
