// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestInfra;

public abstract class BffTestBase : IAsyncDisposable
{
    protected OpenIdConnectOptions DefaultOidcClient;

    protected TestData The;
    protected TestDataBuilder Some;

    private bool _initialized;

    // Keep a list of frontends that are added before initialization
    private readonly List<BffFrontend> _frontendBuffer = new();


    protected BffTestBase(ITestOutputHelper output)
    {
        Context = new TestHostContext(output);

        IdentityServer = new IdentityServerTestHost(Context);
        The = Context.The;
        The.Authority = IdentityServer.Url();

        DefaultOidcClient = new OpenIdConnectOptions()
        {
            ClientId = "bff",
            ClientSecret = The.ClientSecret,
            ResponseType = The.ResponseType,
            ResponseMode = The.ResponseMode,
        };


        Api = new ApiHost(Context, IdentityServer);
        Bff = new BffTestHost(Context);
        Cdn = new CdnHost(Context);
        IdentityServer.AddClient(DefaultOidcClient.ClientId, Bff.Url());
        Some = Context.Some;
    }

    protected void ConfigureBff(BffSetupType setup,
        Action<CookieAuthenticationOptions>? configureCookies = null,
        Action<OpenIdConnectOptions>? configureOpenIdConnect = null
        )
    {
        if (setup == BffSetupType.BffWithFrontend)
        {
            // We're using a frontend to configure the BFF
            // This automatically adds the middleware needed to configure BFF
            AddOrUpdateFrontend(Some.BffFrontend() with
            {
                ConfigureCookieOptions = options =>
                {
                    configureCookies?.Invoke(options);
                },
                ConfigureOpenIdConnectOptions = configureOpenIdConnect ?? The.DefaultOpenIdConnectConfiguration
            });
        }
        else if (setup == BffSetupType.ManuallyConfiguredBff)
        {
            // Old style setup. Explicitly configuring the authentication including cookie, and openid connect
            IdentityServer.AddClient(The.ClientId, Bff.Url());

            Bff.OnConfigureServices += services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultScheme = "cookie";
                        options.DefaultChallengeScheme = "oidc";
                        options.DefaultSignOutScheme = "oidc";
                    })
                    .AddCookie("cookie", options =>
                    {
                        configureCookies?.Invoke(options);
                    })
                    .AddOpenIdConnect("oidc", configureOpenIdConnect
                                              ?? The.DefaultOpenIdConnectConfiguration);
            };
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(setup), setup, null);

        }
    }

    protected virtual void Initialize()
    {

    }

    protected TestHostContext Context { get; }

    protected CdnHost Cdn { get; }
    protected ApiHost Api { get; }
    protected BffTestHost Bff { get; }
    protected IdentityServerTestHost IdentityServer { get; }
    protected SimulatedInternet Internet => Context.Internet;

    public virtual async Task InitializeAsync()
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Already Initialized");
        }

        _initialized = true;

        await Api.InitializeAsync();
        await Bff.InitializeAsync();
        await IdentityServer.InitializeAsync();
        await Cdn.InitializeAsync();

        ProcessFrontendBuffer();

        Internet.AddHandler(Api);
        Internet.AddHandler(Cdn);
        Internet.AddHandler(Bff);
        Internet.AddHandler(IdentityServer);
        Initialize();
    }

    private void ProcessFrontendBuffer()
    {
        // add all frontends that were added before initialization
        foreach (var frontend in _frontendBuffer)
        {
            AddOrUpdateFrontend(frontend);
        }
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsyncCore();

        await Cdn.DisposeAsync();
        await Api.DisposeAsync();
        await Bff.DisposeAsync();
        await IdentityServer.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    public async Task DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

    protected void AddOrUpdateFrontend(BffFrontend frontend)
    {
        if (!_initialized)
        {
            _frontendBuffer.Add(frontend);
            return;
        }

        Bff.AddOrUpdateFrontend(frontend);
        IdentityServer.AddClientFor(frontend, Bff.Url());
    }


    public enum BffSetupType
    {
        /// <summary>
        /// The BFF is configured manually (V3 style)
        /// </summary>
        ManuallyConfiguredBff,

        /// <summary>
        /// The BFF is configured using a frontend (V4 style).
        /// </summary>
        BffWithFrontend
    }

    /// <summary>
    /// There are multiple ways to configure the BFF that should be functionally identical. 
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<object[]> AllSetups()
    {
        yield return [BffSetupType.BffWithFrontend];
        yield return [BffSetupType.ManuallyConfiguredBff];
    }

}

