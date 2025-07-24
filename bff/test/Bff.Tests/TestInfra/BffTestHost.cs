// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Builder;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Forwarder;

namespace Duende.Bff.Tests.TestInfra;

public class BffTestHost(TestHostContext context, IdentityServerTestHost identityServer)
    : TestHost(context, new Uri("https://bff"))
{
    public readonly string DefaultRootResponse = "Default response from root";
    private BffHttpClient _browserClient = null!;
    public BffOptions BffOptions => Resolve<IOptions<BffOptions>>().Value;

    public string? LicenseKey = null;

    /// <summary>
    /// Should a default response for "/" be mapped?
    /// When logging in, you'll return to '/'. This should return just an 'ok' response. 
    /// </summary>
    public bool MapGetForRoot { get; set; } = true;

    public bool EnableBackChannelHandler { get; set; } = true;
    public event Action<IBffServicesBuilder> OnConfigureBff = _ => { };

    public override void Initialize()
    {
        var baseAddress = Url();
        BrowserClient = BuildBrowserClient(baseAddress);

        OnConfigureServices += services =>
        {
            services.AddSingleton<IForwarderHttpClientFactory>(
                new CallbackForwarderHttpClientFactory(context => new HttpMessageInvoker(Internet, false)));

            var builder = services.AddBff(options =>
            {
                if (EnableBackChannelHandler)
                {
                    options.BackchannelHttpHandler = Internet;
                }

                options.LicenseKey = LicenseKey;
            });

            OnConfigureBff(builder);
        };

        OnConfigureApp += app =>
        {
            if (MapGetForRoot)
            {
                app.MapGet("/", () => DefaultRootResponse);
            }
        };
    }

    public BffHttpClient BuildBrowserClient(Uri baseAddress, CookieContainer? cookieContainer = null)
    {
        cookieContainer ??= new CookieContainer();
        var cookieHandler = new CookieHandler(Internet, cookieContainer);
        var redirectHandler = new RedirectHandler(WriteOutput)
        {
            InnerHandler = cookieHandler
        };

        return new BffHttpClient(redirectHandler, cookieContainer, identityServer)
        {
            BaseAddress = baseAddress
        };
    }

    protected override void ConfigureApp(IApplicationBuilder app)
    {
        app.UseRouting();
        app.Use(async (c, n) =>
        {
            await n();
        });
        app.UseAuthentication();
        app.Use(async (c, n) =>
        {
            await n();
            Console.WriteLine();
        });
        app.UseAuthorization();

        app.UseBff();
        base.ConfigureApp(app);
    }

    public BffHttpClient BrowserClient
    {
        get => _browserClient ?? throw new InvalidOperationException("Not yet initialized");
        private set => _browserClient = value;
    }

    public void AddOrUpdateFrontend(BffFrontend frontend) => Resolve<FrontendCollection>().AddOrUpdate(frontend);
}
