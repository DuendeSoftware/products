// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.Builder;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Licensing;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Forwarder;

namespace Duende.Bff.Tests.TestInfra;

public class BffTestHost(TestHostContext context, IdentityServerTestHost identityServer)
    : TestHost(context, new Uri("https://bff"))
{
    public readonly string DefaultRootResponse = "Default response from root";
    private BffHttpClient _browserClient = null!;
    public BffOptions BffOptions => Resolve<IOptions<BffOptions>>().Value;
    public Action<BffOptions> OnConfigureBffOptions = _ => { };

    public void SetLicenseKey(string? value)
    {
        _licenseClaims = [];
        _licenseKey = value;
    }

    /// <summary>
    /// Should a default response for "/" be mapped?
    /// When logging in, you'll return to '/'. This should return just an 'ok' response. 
    /// </summary>
    public bool MapGetForRoot { get; set; } = true;

    public bool EnableBackChannelHandler { get; set; } = true;
    public event Action<IBffServicesBuilder> OnConfigureBff = _ => { };

    private Claim[]? _licenseClaims = null;
    private string? _licenseKey = null;

    public void ConfigureLicense(Claim[] claims) => _licenseClaims = claims.ToArray();

    public override void Initialize()
    {
        var baseAddress = Url();
        BrowserClient = BuildBrowserClient(baseAddress);

        OnConfigureServices += services =>
        {
            if (_licenseClaims == null)
            {
                _licenseClaims = Some.AllowAllLicenseClaims();
            }

            if (_licenseClaims.Any() && _licenseKey == null)
            {
                services.AddSingleton<LicenseValidator>(sp => new LicenseValidator(
                    logger: sp.GetRequiredService<ILogger<LicenseValidator>>(),
                    claims: new ClaimsPrincipal(new ClaimsIdentity(_licenseClaims)),
                    timeProvider: The.Clock));
            }

            services.AddSingleton<IForwarderHttpClientFactory>(
                new CallbackForwarderHttpClientFactory(context => new HttpMessageInvoker(Internet, false)));

            var builder = services.AddBff(options =>
            {
                if (EnableBackChannelHandler)
                {
                    options.BackchannelHttpHandler = Internet;
                }

                OnConfigureBffOptions(options);
                options.LicenseKey = _licenseKey;
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
