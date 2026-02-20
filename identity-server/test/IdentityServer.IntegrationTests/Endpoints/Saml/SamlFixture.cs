// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

internal class SamlFixture : IAsyncLifetime
{
    public SamlData Data = new SamlData();
    public SamlDataBuilder Builder => new SamlDataBuilder(Data);

    public const string StableSigningCert =
        "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";

    public Action<IdentityServerOptions> ConfigureIdentityServerOptions = _ => { };

    public Action<SamlOptions> ConfigureSamlOptions = _ => { };

    public Action<IServiceCollection> ConfigureServices = _ => { };

    private List<SamlServiceProvider> _serviceProviders = [];
    private bool _isInitialized = false;

    /// <summary>
    /// Gets the list of service providers to seed during initialization.
    /// IMPORTANT: This property can only be accessed before InitializeAsync is called.
    /// After initialization, use AddServiceProviderAsync or ClearServiceProvidersAsync methods.
    /// </summary>
    public List<SamlServiceProvider> ServiceProviders
    {
        get
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException(
                    "Cannot access ServiceProviders after initialization. " +
                    "Use AddServiceProviderAsync() to add service providers after the fixture has been initialized, " +
                    "or ClearServiceProvidersAsync() to remove all service providers.");
            }
            return _serviceProviders;
        }
    }

    public DateTimeOffset Now => Data.Now;

    public Uri LoginUrl = new Uri("/account/login", UriKind.Relative);

    public Uri ConsentUrl = new Uri("/consent", UriKind.Relative);

    public Uri SignInCallbackUrl = new Uri("/saml/signin_callback", UriKind.Relative);

    public Uri LogoutUrl = new Uri("/account/logout", UriKind.Relative);

    public ClaimsPrincipal? UserToSignIn { get; set; }

    public bool? UserMetRequestedAuthnContextRequirements { get; set; }

    public AuthenticationProperties? PropsToSignIn { get; set; }

    private IdentityServerPipeline _pipeline = null!;

    public IdentityServerPipeline Pipeline => _pipeline;

    public BrowserClient Client { get; private set; } = null!;

    public BrowserClient NonRedirectingClient { get; private set; } = null!;

    public string Url(string path = "")
    {
        if (!path.StartsWith('/') && !string.IsNullOrEmpty(path))
        {
            path = '/' + path;
        }

        return IdentityServerPipeline.BaseUrl + path;
    }

    public T Get<T>() where T : notnull => _pipeline.Resolve<T>();

    public async ValueTask InitializeAsync()
    {
        var selfSignedCertificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(StableSigningCert), null);

        _pipeline = new IdentityServerPipeline();

        _pipeline.OnPreConfigureServices += services =>
        {
            services.AddSingleton<TimeProvider>(Data.FakeTimeProvider);
            services.AddSingleton<IDistributedCache>(sp => new FakeDistributedCache(sp.GetRequiredService<TimeProvider>()));
            services.AddRouting();
            services.AddAuthorization();
        };

        _pipeline.OnPostConfigureServices += services =>
        {
            // Configure IdentityServer options (pipeline already calls AddIdentityServer)
            services.Configure<IdentityServerOptions>(options =>
            {
                options.UserInteraction.LoginUrl = LoginUrl.ToString();
                options.UserInteraction.LogoutUrl = LogoutUrl.ToString();
                options.UserInteraction.ConsentUrl = ConsentUrl.ToString();
                options.KeyManagement.Enabled = false;  // Disable key management to use our custom credential
            });
            services.Configure(ConfigureIdentityServerOptions);

            // Configure SAML options
            services.Configure(ConfigureSamlOptions);

            // Replace the developer signing credential with our X509 certificate
            // Remove the ISigningCredentialStore registration added by AddDeveloperSigningCredential
            var signingCredentialDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISigningCredentialStore));
            if (signingCredentialDescriptor != null)
            {
                services.Remove(signingCredentialDescriptor);
            }

            // Add our X509 signing credential
            services.AddIdentityServerBuilder()
                .AddSigningCredential(selfSignedCertificate)
                .AddProfileService<DefaultProfileService>()
                .AddSaml()
                .AddInMemorySamlServiceProviders(_serviceProviders);

            ConfigureServices(services);

            services.AddProblemDetails(opt => opt.CustomizeProblemDetails = context =>
            {
                if (context.Exception is BadHttpRequestException ex)
                {
                    context.HttpContext.Response.StatusCode = 400;
                    context.ProblemDetails.Detail = ex.Message;
                    context.ProblemDetails.Status = 400;
                }
            });
        };

        _pipeline.OnPreConfigure += app =>
        {
            app.UseExceptionHandler("/error");
        };

        _pipeline.OnPostConfigure += app =>
        {
            // Error handling endpoint
            app.Map("/error", path =>
            {
                path.Run(async context =>
                {
                    var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                    if (exceptionFeature?.Error is Microsoft.AspNetCore.Http.BadHttpRequestException badRequestEx)
                    {
                        context.Response.StatusCode = badRequestEx.StatusCode;
                        context.Response.ContentType = "application/problem+json";
                        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                        {
                            Status = badRequestEx.StatusCode,
                            Title = "Bad Request",
                            Detail = badRequestEx.Message
                        });
                    }
                    else
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Internal Server Error");
                    }
                });
            });

            app.Map(LoginUrl.ToString(), path =>
            {
                path.Run(ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

            app.Map(ConsentUrl.ToString(), path =>
            {
                path.Run(ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

            app.Map(LogoutUrl.ToString(), path =>
            {
                path.Run(ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

            app.Map("/__signin", path =>
            {
                path.Run(async ctx =>
                {
                    var samlInteractionService = ctx.RequestServices.GetRequiredService<ISamlInteractionService>();
                    var props = PropsToSignIn ?? new AuthenticationProperties();
                    if (UserToSignIn?.Identity == null)
                    {
                        throw new InvalidOperationException(
                            $"Must set {nameof(UserToSignIn)} prior to signin and must have an identity");
                    }

                    await ctx.SignInAsync(UserToSignIn, props);

                    if (UserMetRequestedAuthnContextRequirements.HasValue)
                    {
                        await samlInteractionService.StoreRequestedAuthnContextResultAsync(
                            UserMetRequestedAuthnContextRequirements.Value, ctx.RequestAborted);
                    }

                    ctx.Response.StatusCode = 204;
                });
            });

            app.Map("/__signout", path =>
            {
                path.Run(async ctx =>
                {
                    await ctx.SignOutAsync();
                    ctx.Response.StatusCode = 204;
                });
            });

            app.Map("/__authentication-request", path =>
            {
                path.Run(async ctx =>
                {
                    var samlInteractionService = ctx.RequestServices.GetRequiredService<ISamlInteractionService>();
                    var authenticationRequest =
                        await samlInteractionService.GetAuthenticationRequestContextAsync(CancellationToken.None);

                    if (authenticationRequest == null)
                    {
                        throw new InvalidOperationException("Could not find authentication request");
                    }

                    await ctx.Response.WriteAsJsonAsync(authenticationRequest.RequestedAuthnContext);
                });
            });
        };

        _pipeline.Initialize(enableLogging: true);

        // Mark as initialized after seeding
        _isInitialized = true;

        // Create two BrowserClient instances with different redirect behaviors
        Client = _pipeline.BrowserClient;
        Client.BaseAddress = new Uri(IdentityServerPipeline.BaseUrl);

        NonRedirectingClient = new BrowserClient(new BrowserHandler(_pipeline.Handler) { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(IdentityServerPipeline.BaseUrl)
        };
    }

    public async ValueTask DisposeAsync() =>
        // IdentityServerPipeline doesn't implement IAsyncDisposable, so nothing to dispose
        await Task.CompletedTask;

    /// <summary>
    /// Removes all service providers from the fixture after initialization.
    /// </summary>
    public void ClearServiceProvidersAsync() => _serviceProviders.Clear();
}
