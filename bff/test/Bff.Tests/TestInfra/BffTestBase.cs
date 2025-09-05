// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
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
        Bff = new BffTestHost(Context, IdentityServer);
        Cdn = new CdnHost(Context);
        IdentityServer.AddClient(DefaultOidcClient.ClientId, Bff.Url());
        Some = Context.Some;
    }

    protected virtual async Task ConfigureBff(
        BffSetupType setup,
        Action<CookieAuthenticationOptions>? configureCookie = null,
        Action<OpenIdConnectOptions>? configureOpenIdConnect = null)
    {
        // This method is used to configure the BFF in different ways depending on the setup type.
        Action<OpenIdConnectOptions> openIdConfiguration = opt =>
        {
            (configureOpenIdConnect ?? The.DefaultOpenIdConnectConfiguration).Invoke(opt);

            if (_customUserClaims.Any())
            {
                AddCustomUserClaims(opt);
            }
        };


        if (setup == BffSetupType.BffWithFrontend)
        {
            // We're using a frontend to configure the BFF
            // This automatically adds the middleware needed to configure BFF
            CurrentFrontend = Some.BffFrontend() with
            {
                ConfigureCookieOptions = options =>
                {
                    configureCookie?.Invoke(options);
                },
                ConfigureOpenIdConnectOptions = openIdConfiguration
            };
            AddOrUpdateFrontend(CurrentFrontend);
        }
        else if (setup == BffSetupType.V4Bff)
        {
            IdentityServer.AddClient(The.ClientId, Bff.Url());
            Bff.OnConfigureBff += bff =>
            {
                bff.ConfigureOpenIdConnect(openIdConfiguration);
                bff.ConfigureCookies(options =>
                {
                    configureCookie?.Invoke(options);
                });
            };
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
                        configureCookie?.Invoke(options);
                    })
                    .AddOpenIdConnect("oidc", openIdConfiguration);
            };
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(setup), setup, null);

        }

        await InitializeAsync();
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
        List<Uri> baseUris = [Bff.Url()];
        if (frontend.MatchingCriteria.MatchingHostHeader != null)
        {
            baseUris.Add(frontend.MatchingCriteria.MatchingHostHeader.ToUri());
        }
        IdentityServer.AddClientFor(frontend, baseUris);
    }


    public enum BffSetupType
    {
        /// <summary>
        /// The BFF is configured manually (V3 style)
        /// </summary>
        ManuallyConfiguredBff,

        /// <summary>
        /// The BFF is configured using an explicit frontend (V4 style).
        /// </summary>
        BffWithFrontend,

        /// <summary>
        /// The BFF is configured using v4 style setup, but without a frontend
        /// This creates an implicit frontend. 
        /// </summary>
        V4Bff
    }

    /// <summary>
    /// There are multiple ways to configure the BFF that should be functionally identical. 
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<object[]> AllSetups()
    {
        yield return [BffSetupType.BffWithFrontend];
        yield return [BffSetupType.ManuallyConfiguredBff];
        yield return [BffSetupType.V4Bff];
    }

    private List<Claim> _customUserClaims = [];
    public BffFrontend? CurrentFrontend;

    protected void AddCustomUserClaims(params Claim[] claims) => _customUserClaims.AddRange(claims);

    private void AddCustomUserClaims(OpenIdConnectOptions opt) =>
        opt.Events.OnTokenValidated = context =>
        {
            // Add custom claims to the identity
            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            foreach (var claim in _customUserClaims)
            {
                identity.AddClaim(claim);
            }

            return Task.CompletedTask;
        };

    protected void AdvanceClock(TimeSpan by) => The.Clock.SetUtcNow(The.Clock.GetUtcNow().Add(by));
}

