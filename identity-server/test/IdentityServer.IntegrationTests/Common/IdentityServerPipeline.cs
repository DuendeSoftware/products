// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace Duende.IdentityServer.IntegrationTests.Common;

public class IdentityServerPipeline
{
    public const string BaseUrl = "https://server";
    public const string LoginPage = BaseUrl + "/account/login";
    public const string LogoutPage = BaseUrl + "/account/logout";
    public const string ConsentPage = BaseUrl + "/account/consent";
    public const string CreateAccountPage = BaseUrl + "/account/create";

    public const string ErrorPage = BaseUrl + "/home/error";

    public const string DeviceAuthorization = BaseUrl + "/connect/deviceauthorization";
    public const string DiscoveryEndpoint = BaseUrl + "/.well-known/openid-configuration";
    public const string DiscoveryKeysEndpoint = BaseUrl + "/.well-known/openid-configuration/jwks";
    public const string AuthorizeEndpoint = BaseUrl + "/connect/authorize";
    public const string BackchannelAuthenticationEndpoint = BaseUrl + "/connect/ciba";
    public const string TokenEndpoint = BaseUrl + "/connect/token";
    public const string TokenMtlsEndpoint = BaseUrl + "/connect/mtls/token";
    public const string RevocationEndpoint = BaseUrl + "/connect/revocation";
    public const string UserInfoEndpoint = BaseUrl + "/connect/userinfo";
    public const string IntrospectionEndpoint = BaseUrl + "/connect/introspect";
    public const string EndSessionEndpoint = BaseUrl + "/connect/endsession";
    public const string EndSessionCallbackEndpoint = BaseUrl + "/connect/endsession/callback";
    public const string CheckSessionEndpoint = BaseUrl + "/connect/checksession";
    public const string ParEndpoint = BaseUrl + "/connect/par";
    public const string ParMtlsEndpoint = BaseUrl + "/connect/mtls/par";


    public const string FederatedSignOutPath = "/signout-oidc";
    public const string FederatedSignOutUrl = BaseUrl + FederatedSignOutPath;

    public IdentityServerOptions Options { get; set; }
    public List<Client> Clients { get; set; } = new List<Client>();
    public Dictionary<string, JsonWebKey> ClientKeys { get; set; } = new Dictionary<string, JsonWebKey>();
    public List<IdentityResource> IdentityScopes { get; set; } = new List<IdentityResource>();
    public List<ApiResource> ApiResources { get; set; } = new List<ApiResource>();
    public List<ApiScope> ApiScopes { get; set; } = new List<ApiScope>();
    public List<TestUser> Users { get; set; } = new List<TestUser>();

    public TestServer Server { get; set; }
    public HttpMessageHandler Handler { get; set; }

    public BrowserClient BrowserClient { get; set; }
    public HttpClient BackChannelClient { get; set; }

    // mTLS support
    public X509Certificate2 ClientCertificate { get; set; }
    public HttpClient MtlsBackChannelClient { get; set; }

    public MockMessageHandler BackChannelMessageHandler { get; set; } = new MockMessageHandler();
    public MockMessageHandler JwtRequestMessageHandler { get; set; } = new MockMessageHandler();

    public MockLogger MockLogger { get; set; } = MockLogger.Create();

    public event Action<IServiceCollection> OnPreConfigureServices = services => { };
    public event Action<IServiceCollection> OnPostConfigureServices = services => { };
    public event Action<IApplicationBuilder> OnPreConfigure = app => { };
    public event Action<IApplicationBuilder> OnPostConfigure = app => { };

    public Func<HttpContext, Task<bool>> OnFederatedSignout;

    public void Initialize(string basePath = null, bool enableLogging = false)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();

                webHost.ConfigureServices(ConfigureServices);

                webHost.Configure(app =>
                {
                    if (basePath != null)
                    {
                        app.Map(basePath, ConfigureApp);
                    }
                    else
                    {
                        ConfigureApp(app);
                    }
                });

                if (enableLogging)
                {
                    // Configure logging so that the logger provider will always use our mock logger
                    // The MockLogger allows us to verify that particular messages were logged.
                    webHost.ConfigureLogging((ctx, b) =>
                        b.Services.AddSingleton<ILoggerProvider>(new MockLoggerProvider(MockLogger)));
                }
            });

        var host = hostBuilder.Start();

        Server = host.GetTestServer();
        Handler = Server.CreateHandler();

        BrowserClient = new BrowserClient(new BrowserHandler(Handler));
        BackChannelClient = new HttpClient(Handler);

        // Initialize mTLS client (will be null until a certificate is set)
        UpdateMtlsClient();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        OnPreConfigureServices(services);

        services.AddAuthentication(opts =>
        {
            opts.AddScheme("external", scheme =>
            {
                scheme.DisplayName = "External";
                scheme.HandlerType = typeof(MockExternalAuthenticationHandler);
            });
            opts.AddScheme("Certificate", scheme =>
            {
                scheme.DisplayName = "Certificate";
                scheme.HandlerType = typeof(MockCertificateAuthenticationHandler);
            });
        });
        services.AddTransient<MockExternalAuthenticationHandler>(svcs =>
        {
            var handler = new MockExternalAuthenticationHandler(svcs.GetRequiredService<IHttpContextAccessor>());
            if (OnFederatedSignout != null)
            {
                handler.OnFederatedSignout = OnFederatedSignout;
            }

            return handler;
        });

        services.AddIdentityServer(options =>
            {
                options.Events = new EventsOptions
                {
                    RaiseErrorEvents = true,
                    RaiseFailureEvents = true,
                    RaiseInformationEvents = true,
                    RaiseSuccessEvents = true
                };
                options.KeyManagement.Enabled = false;

                options.MutualTls.Enabled = true;

                Options = options;
            })
            .AddInMemoryClients(Clients)
            .AddInMemoryIdentityResources(IdentityScopes)
            .AddInMemoryApiResources(ApiResources)
            .AddInMemoryApiScopes(ApiScopes)
            .AddTestUsers(Users)
            .AddDeveloperSigningCredential(persistKey: false)
            .AddMutualTlsSecretValidators()
            .AddLicenseSummary();

        services.AddHttpClient(IdentityServerConstants.HttpClients.BackChannelLogoutHttpClient)
            .AddHttpMessageHandler(() => BackChannelMessageHandler);

        services.AddHttpClient(IdentityServerConstants.HttpClients.JwtRequestUriHttpClient)
            .AddHttpMessageHandler(() => JwtRequestMessageHandler);

        OnPostConfigureServices(services);
    }

    public void ConfigureApp(IApplicationBuilder app)
    {
        ApplicationServices = app.ApplicationServices;

        OnPreConfigure(app);

        // Add mTLS test middleware before IdentityServer middleware
        app.UseMiddleware<MtlsTestMiddleware>();

        app.UseIdentityServer();

        // UI endpoints
        app.Map("/account/create", path =>
        {
            path.Run(ctx => OnCreateAccount(ctx));
        });
        app.Map(Constants.UIConstants.DefaultRoutePaths.Login.EnsureLeadingSlash(), path =>
        {
            path.Run(ctx => OnLogin(ctx));
        });
        app.Map(Constants.UIConstants.DefaultRoutePaths.Logout.EnsureLeadingSlash(), path =>
        {
            path.Run(ctx => OnLogout(ctx));
        });
        app.Map(Constants.UIConstants.DefaultRoutePaths.Consent.EnsureLeadingSlash(), path =>
        {
            path.Run(ctx => OnConsent(ctx));
        });
        app.Map("/custom", path =>
        {
            path.Run(ctx => OnCustom(ctx));
        });
        app.Map(Constants.UIConstants.DefaultRoutePaths.Error.EnsureLeadingSlash(), path =>
        {
            path.Run(ctx => OnError(ctx));
        });

        OnPostConfigure(app);
    }

    public bool LoginWasCalled { get; set; }
    public string LoginReturnUrl { get; set; }
    public AuthorizationRequest LoginRequest { get; set; }
    public ClaimsPrincipal Subject { get; set; }
    public AuthenticationProperties AuthenticationProperties { get; set; }

    private async Task OnLogin(HttpContext ctx)
    {
        LoginWasCalled = true;
        await ReadLoginRequest(ctx);
        await IssueLoginCookie(ctx);
    }

    public bool CreateAccountWasCalled { get; set; }
    public string CreateAccountReturnUrl { get; set; }
    public AuthorizationRequest CreateAccountRequest { get; set; }
    private async Task OnCreateAccount(HttpContext ctx)
    {
        CreateAccountWasCalled = true;
        var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
        CreateAccountReturnUrl = ctx.Request.Query[Options.UserInteraction.CreateAccountReturnUrlParameter].FirstOrDefault();
        CreateAccountRequest = await interaction.GetAuthorizationContextAsync(CreateAccountReturnUrl, ctx.RequestAborted);
        await IssueLoginCookie(ctx);
    }

    private async Task ReadLoginRequest(HttpContext ctx)
    {
        var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
        LoginReturnUrl = ctx.Request.Query[Options.UserInteraction.LoginReturnUrlParameter].FirstOrDefault();
        LoginRequest = await interaction.GetAuthorizationContextAsync(LoginReturnUrl, ctx.RequestAborted);
    }

    private async Task IssueLoginCookie(HttpContext ctx)
    {
        if (Subject != null)
        {
            var props = AuthenticationProperties ?? new AuthenticationProperties();
            await ctx.SignInAsync(Subject, props);
            Subject = null;
            var url = ctx.Request.Query[Options.UserInteraction.LoginReturnUrlParameter].FirstOrDefault();
            if (url != null)
            {
                ctx.Response.Redirect(url);
            }
        }
    }

    public bool LogoutWasCalled { get; set; }
    public LogoutRequest LogoutRequest { get; set; }

    private async Task OnLogout(HttpContext ctx)
    {
        LogoutWasCalled = true;
        await ReadLogoutRequest(ctx);
        await ctx.SignOutAsync();
    }

    private async Task ReadLogoutRequest(HttpContext ctx)
    {
        var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
        LogoutRequest = await interaction.GetLogoutContextAsync(ctx.Request.Query["logoutId"].FirstOrDefault(), ctx.RequestAborted);
    }

    public bool ConsentWasCalled { get; set; }
    public AuthorizationRequest ConsentRequest { get; set; }
    public ConsentResponse ConsentResponse { get; set; }

    private async Task OnConsent(HttpContext ctx)
    {
        ConsentWasCalled = true;
        await ReadConsentMessage(ctx);
        await CreateConsentResponse(ctx);
    }
    private async Task ReadConsentMessage(HttpContext ctx)
    {
        var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
        ConsentRequest = await interaction.GetAuthorizationContextAsync(ctx.Request.Query["returnUrl"].FirstOrDefault(), ctx.RequestAborted);
    }
    private async Task CreateConsentResponse(HttpContext ctx)
    {
        if (ConsentRequest != null && ConsentResponse != null)
        {
            var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
            await interaction.GrantConsentAsync(ConsentRequest, ConsentResponse, ctx.RequestAborted);
            ConsentResponse = null;

            var url = ctx.Request.Query[Options.UserInteraction.ConsentReturnUrlParameter].FirstOrDefault();
            if (url != null)
            {
                ctx.Response.Redirect(url);
            }
        }
    }

    public bool CustomWasCalled { get; set; }
    public AuthorizationRequest CustomRequest { get; set; }

    private async Task OnCustom(HttpContext ctx)
    {
        CustomWasCalled = true;
        var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
        CustomRequest = await interaction.GetAuthorizationContextAsync(ctx.Request.Query[Options.UserInteraction.ConsentReturnUrlParameter].FirstOrDefault(), ctx.RequestAborted);
    }

    public bool ErrorWasCalled { get; set; }
    public ErrorMessage ErrorMessage { get; set; }
    public IServiceProvider ApplicationServices { get; private set; }

    private async Task OnError(HttpContext ctx)
    {
        ErrorWasCalled = true;
        await ReadErrorMessage(ctx);
    }

    private async Task ReadErrorMessage(HttpContext ctx)
    {
        var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
        ErrorMessage = await interaction.GetErrorContextAsync(ctx.Request.Query["errorId"].FirstOrDefault(), ctx.RequestAborted);
    }

    /* helpers */
    public async Task LoginAsync(ClaimsPrincipal subject, AuthenticationProperties authenticationProperties = null)
    {
        var old = BrowserClient.AllowAutoRedirect;
        BrowserClient.AllowAutoRedirect = false;

        Subject = subject;
        AuthenticationProperties = authenticationProperties;
        await BrowserClient.GetAsync(LoginPage);

        BrowserClient.AllowAutoRedirect = old;
    }

    public async Task LoginAsync(string subject, AuthenticationProperties authenticationProperties = null) => await LoginAsync(new IdentityServerUser(subject).CreatePrincipal(), authenticationProperties);
    public async Task LogoutAsync()
    {
        var old = BrowserClient.AllowAutoRedirect;
        BrowserClient.AllowAutoRedirect = false;

        await BrowserClient.GetAsync(LogoutPage);

        BrowserClient.AllowAutoRedirect = old;
    }

    public void RemoveLoginCookie() => BrowserClient.RemoveCookie(BaseUrl, Options.Authentication.CookieName);
    public void RemoveSessionCookie() => BrowserClient.RemoveCookie(BaseUrl, IdentityServerConstants.DefaultCheckSessionCookieName);
    public Cookie GetSessionCookie() => BrowserClient.GetCookie(BaseUrl, IdentityServerConstants.DefaultCheckSessionCookieName);

    public string CreateAuthorizeUrl(
        string clientId = null,
        string responseType = null,
        string scope = null,
        string redirectUri = null,
        string state = null,
        string nonce = null,
        string loginHint = null,
        string acrValues = null,
        string responseMode = null,
        string codeChallenge = null,
        string codeChallengeMethod = null,
        string requestUri = null,
        object extra = null)
    {
        var url = new RequestUrl(AuthorizeEndpoint).CreateAuthorizeUrl(
            clientId: clientId,
            responseType: responseType,
            scope: scope,
            redirectUri: redirectUri,
            state: state,
            nonce: nonce,
            loginHint: loginHint,
            acrValues: acrValues,
            responseMode: responseMode,
            codeChallenge: codeChallenge,
            codeChallengeMethod: codeChallengeMethod,
            requestUri: requestUri,
            extra: Parameters.FromObject(extra));
        return url;
    }
    public async Task<(JsonDocument, HttpStatusCode)> PushAuthorizationRequestAsync(
        Dictionary<string, string> parameters)
    {
        var httpResponse = await BackChannelClient.PostAsync(ParEndpoint,
            new FormUrlEncodedContent(parameters));
        var statusCode = httpResponse.StatusCode;
        var rawContent = await httpResponse.Content.ReadAsStringAsync();
        var parsed = rawContent.IsPresent() ? JsonDocument.Parse(rawContent) : null;
        return (parsed, statusCode);
    }

    public async Task<(JsonDocument, HttpStatusCode)> PushAuthorizationRequestAsync(
        string clientId = "client1",
        string clientSecret = "secret",
        string responseType = "id_token",
        string scope = "openid profile",
        string redirectUri = "https://client1/callback",
        string nonce = "123_nonce",
        string state = "123_state",
        Dictionary<string, string> extra = null
    )
    {
        var parameters = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "response_type", responseType },
                { "scope", scope },
                { "redirect_uri", redirectUri },
                { "nonce", nonce },
                { "state", state }
            };

        if (extra != null)
        {
            foreach (var (key, value) in extra)
            {
                parameters[key] = value;
            }
        }

        return await PushAuthorizationRequestAsync(parameters);
    }

    public async Task<(JsonDocument, HttpStatusCode)> PushAuthorizationRequestUsingJarAsync(
        string clientId = "client2",
        string clientSecret = "secret",
        string responseType = "id_token",
        string scope = "openid profile",
        string redirectUri = "https://client2/callback",
        string nonce = "123_nonce",
        string state = "123_state",
        DateTime? expires = null,
        Dictionary<string, string> extraJwt = null,
        Dictionary<string, string> extraForm = null
    )
    {
        var jwtPayload = new Dictionary<string, string>
        {
            { "response_type", responseType },
            { "client_id", clientId },
            { "redirect_uri", redirectUri },
            { "scope", scope },
            { "state", state },
            { "nonce", nonce }
        };

        if (extraJwt != null)
        {
            foreach (var (key, value) in extraJwt)
            {
                jwtPayload[key] = value;
            }
        }

        if (!ClientKeys.TryGetValue(clientId, out var securityKey))
        {
            throw new InvalidOperationException("Client key not found");
        }

        expires ??= DateTime.UtcNow.AddMinutes(10);
        var jwt = new JwtSecurityToken(
            new JwtHeader(new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256)),
            new JwtPayload(clientId, BaseUrl,
                jwtPayload.Select(x => new Claim(x.Key, x.Value)),
                notBefore: null,
                expires: expires));

        var jwtHandler = new JwtSecurityTokenHandler();
        var jar = jwtHandler.WriteToken(jwt);

        var parameters = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "request", jar }
        };

        if (extraForm != null)
        {
            foreach (var (key, value) in extraForm)
            {
                parameters[key] = value;
            }
        }

        return await PushAuthorizationRequestAsync(parameters);
    }

    public Duende.IdentityModel.Client.AuthorizeResponse ParseAuthorizationResponseUrl(string url) => new Duende.IdentityModel.Client.AuthorizeResponse(url);

    public async Task<Duende.IdentityModel.Client.AuthorizeResponse> RequestAuthorizationEndpointAsync(
        string clientId,
        string responseType,
        string scope = null,
        string redirectUri = null,
        string state = null,
        string nonce = null,
        string loginHint = null,
        string acrValues = null,
        string responseMode = null,
        string codeChallenge = null,
        string codeChallengeMethod = null,
        string requestUri = null,
        object extra = null)
    {
        var old = BrowserClient.AllowAutoRedirect;
        BrowserClient.AllowAutoRedirect = false;

        var url = CreateAuthorizeUrl(clientId, responseType, scope, redirectUri, state, nonce, loginHint, acrValues, responseMode, codeChallenge, codeChallengeMethod, requestUri, extra);
        var result = await BrowserClient.GetAsync(url);
        result.StatusCode.ShouldBe(HttpStatusCode.SeeOther);

        BrowserClient.AllowAutoRedirect = old;

        var redirect = result.Headers.Location.ToString();
        if (redirect.StartsWith(IdentityServerPipeline.ErrorPage))
        {
            // request error page in pipeline so we can get error info
            await BrowserClient.GetAsync(redirect);

            // no redirect to client
            return null;
        }

        return new Duende.IdentityModel.Client.AuthorizeResponse(redirect);
    }

    public T Resolve<T>() =>
        // create throw-away scope
        ApplicationServices.CreateScope().ServiceProvider.GetRequiredService<T>();

    /* mTLS helpers */
    public void SetClientCertificate(X509Certificate2 certificate)
    {
        ClientCertificate = certificate;
        UpdateMtlsClient();
    }

    private void UpdateMtlsClient()
    {
        MtlsBackChannelClient?.Dispose();

        if (ClientCertificate != null)
        {
            var mtlsHandler = new MtlsMessageHandler(Handler, ClientCertificate);
            MtlsBackChannelClient = new HttpClient(mtlsHandler)
            {
                BaseAddress = new Uri(BaseUrl)
            };
        }
        else
        {
            MtlsBackChannelClient = null;
        }
    }

    public HttpClient GetMtlsClient()
    {
        if (MtlsBackChannelClient == null)
        {
            throw new InvalidOperationException("No client certificate has been set. Call SetClientCertificate() first.");
        }
        return MtlsBackChannelClient;
    }
}

public class MockMessageHandler : DelegatingHandler
{
    public bool InvokeWasCalled { get; set; }
    public Func<HttpRequestMessage, Task> OnInvoke { get; set; }
    public HttpResponseMessage Response { get; set; } = new HttpResponseMessage(HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CT ct)
    {
        InvokeWasCalled = true;

        if (OnInvoke != null)
        {
            await OnInvoke.Invoke(request);
        }
        return Response;
    }
}

public class MockExternalAuthenticationHandler :
    IAuthenticationHandler,
    IAuthenticationSignInHandler,
    IAuthenticationRequestHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private HttpContext HttpContext => _httpContextAccessor.HttpContext;

    public Func<HttpContext, Task<bool>> OnFederatedSignout =
        async context =>
        {
            await context.SignOutAsync();
            return true;
        };

    public MockExternalAuthenticationHandler(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public async Task<bool> HandleRequestAsync()
    {
        if (HttpContext.Request.Path == IdentityServerPipeline.FederatedSignOutPath)
        {
            return await OnFederatedSignout(HttpContext);
        }

        return false;
    }

    public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(AuthenticationProperties properties) => Task.CompletedTask;

    public Task ForbidAsync(AuthenticationProperties properties) => Task.CompletedTask;

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;

    public Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties properties) => Task.CompletedTask;

    public Task SignOutAsync(AuthenticationProperties properties) => Task.CompletedTask;
}

public class MockCertificateAuthenticationHandler : IAuthenticationHandler
{
    private HttpContext _context;
    private string _scheme;

    public Task<AuthenticateResult> AuthenticateAsync()
    {
        if (_context?.Features.Get<ITlsConnectionFeature>() is { ClientCertificate: not null })
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([])), _scheme)));
        }
        return Task.FromResult(AuthenticateResult.Fail("No client certificate set."));
    }

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
    {
        _scheme = scheme.Name;
        _context = context;
        return Task.CompletedTask;
    }

    public Task ChallengeAsync(AuthenticationProperties properties) => Task.CompletedTask;

    public Task ForbidAsync(AuthenticationProperties properties) => Task.CompletedTask;
}
