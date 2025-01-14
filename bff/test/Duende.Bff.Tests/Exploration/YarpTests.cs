using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Tests.Exploration
{
    public class YarpTests
    {
        [Fact]
        public async Task Can_proxy_two_requests()
        {
            await using var apiServer = await GetApiServer();
            await using var proxy = await GetProxyServer(apiServer.GetServerUri());

            var client = new HttpClient()
            {
                BaseAddress = proxy.GetServerUri()
            };
            var result = await client.GetAsync("/proxy/api");
            result.EnsureSuccessStatusCode();
        }

        private static async Task<Server> GetApiServer()
        {
            Server apiServer = null;
            try
            {
                apiServer = new Server(
                    configureServices: (services) =>
                    {

                    }, 
                    configure: app =>
                    {
                        app.MapGet("/api", () => "ok");
                    });

                await apiServer.Start();
                return apiServer;
            }
            catch
            {
                if (apiServer != null) await apiServer.DisposeAsync();
                throw;
            }
        }


        private static async Task<Server> GetProxyServer(Uri api)
        {
            Server apiServer = null;

            var requestConfig = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };


            try
            {
                apiServer = new Server(
                    configureServices: (services) =>
                    {
                        services.AddHttpForwarder();
                        services.AddReverseProxy();
                    },
                    configure: app =>
                    {
                        var transformer = app.Services.GetRequiredService<ITransformBuilder>()
                            .Create(context =>
                            {
                                context.RequestTransforms.Add(new PathStringTransform(PathStringTransform.PathTransformMode.RemovePrefix, "/proxy"));
                            });

                        var httpClient = app.Services.GetRequiredService<IForwarderHttpClientFactory>()
                            .CreateClient(new ForwarderHttpClientContext { NewConfig = HttpClientConfig.Empty });

                        // When using IHttpForwarder for direct forwarding you are responsible for routing, destination discovery, load balancing, affinity, etc..
                        // For an alternate example that includes those features see BasicYarpSample.
                        app.Map("/proxy/{**catch-all}", async (HttpContext httpContext, IHttpForwarder forwarder) =>
                        {
                            var error = await forwarder.SendAsync(httpContext, api.ToString(),
                                httpClient, requestConfig, transformer);
                            // Check if the operation was successful
                            if (error != ForwarderError.None)
                            {
                                var errorFeature = httpContext.GetForwarderErrorFeature();
                                var exception = errorFeature.Exception;
                            }
                        });
                        
                    });

                await apiServer.Start();
                return apiServer;
            }
            catch
            {
                if (apiServer != null) await apiServer.DisposeAsync();
                throw;
            }
        }
    }

    public class Server : IAsyncDisposable
    {
        private WebApplicationBuilder _builder = WebApplication.CreateBuilder();
        private WebApplication App;

        public Server(Action<IServiceCollection> configureServices, Action<WebApplication> configure)
        {
            
            _builder.WebHost.UseKestrel(
                    opt =>
                    {
                        opt.Limits.KeepAliveTimeout = TimeSpan.MaxValue;
                        opt.Limits.RequestHeadersTimeout = TimeSpan.MaxValue;
                    })
                .UseUrls("http://127.0.0.1:0") // port zero to use random dynamic port
                ;


            var services = _builder.Services;
            services.AddRouting();
            configureServices(services);

            App = _builder.Build();

            App.UseRouting();
            configure(App);
        }

        public Uri GetServerUri()
        {
            var address = (App as IApplicationBuilder).ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
            var uri = new Uri(address!.Addresses.First());
            return uri;
        }

        public async Task Start(CancellationToken ct = default)
        {
            await App.StartAsync(ct);
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            if (App != null)
            {
                await App.StopAsync(ct);
                await App.DisposeAsync();
                App = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);
        }
    }
}
