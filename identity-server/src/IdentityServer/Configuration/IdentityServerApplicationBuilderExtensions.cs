// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Reflection;
using System.Runtime.InteropServices;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Licensing;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Saml.Endpoints;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Pipeline extension methods for adding IdentityServer
/// </summary>
public static class IdentityServerApplicationBuilderExtensions
{
    /// <summary>
    /// Adds IdentityServer to the ASP.NET Core request pipeline. This registers the middleware components
    /// required to handle all IdentityServer protocol endpoints (authorize, token, discovery, userinfo, etc.),
    /// validates the IdentityServer configuration and license at startup, and sets up CORS, mutual TLS,
    /// and dynamic external provider authentication.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add IdentityServer middleware to.</param>
    /// <param name="options">Optional <see cref="IdentityServerMiddlewareOptions"/> to customize how the
    /// ASP.NET Core authentication middleware is inserted into the pipeline. If not provided, the default
    /// behavior calls <c>UseAuthentication()</c> automatically.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> so that additional middleware can be chained.</returns>
    public static IApplicationBuilder UseIdentityServer(this IApplicationBuilder app, IdentityServerMiddlewareOptions? options = null)
    {
        app.Validate();

        app.UseMiddleware<BaseUrlMiddleware>();

        app.ConfigureCors();

        app.UseMiddleware<DynamicSchemeAuthenticationMiddleware>();

        // it seems ok if we have UseAuthentication more than once in the pipeline --
        // this will just re-run the various callback handlers and the default authN
        // handler, which just re-assigns the user on the context. claims transformation
        // will run twice, since that's not cached (whereas the authN handler result is)
        // related: https://github.com/aspnet/Security/issues/1399
        if (options == null)
        {
            options = new IdentityServerMiddlewareOptions();
        }

        options.AuthenticationMiddleware(app);

        app.UseMiddleware<MutualTlsEndpointMiddleware>();
        app.UseMiddleware<IdentityServerMiddleware>();

        return app;
    }

    internal static void Validate(this IApplicationBuilder app)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var logger = loggerFactory.CreateLogger("Duende.IdentityServer.Startup");
        logger.LogInformation("Starting Duende IdentityServer version {version} ({netversion})",
            typeof(IdentityServerMiddleware).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            RuntimeInformation.FrameworkDescription);

        var scopeFactory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();

        using (var scope = scopeFactory.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;

            var options = serviceProvider.GetRequiredService<IdentityServerOptions>();

            var licenseValidator = serviceProvider.GetRequiredService<IdentityServerLicenseValidator>();
            licenseValidator.ValidateLicense();

            if (options.KeyManagement.Enabled)
            {
                var licenseUsage = serviceProvider.GetRequiredService<LicenseUsageTracker>();
                licenseUsage.KeyManagementUsed();

                if (!licenseValidator.ValidateKeyManagement())
                {
                    IdentityServerLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Key Management feature.");
                }
            }

            if (serviceProvider.GetService<IServerSideSessionsMarker>() != null)
            {
                if (!licenseValidator.ValidateServerSideSessions())
                {
                    IdentityServerLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Server-Side Sessions feature.");
                }
            }

            if (serviceProvider.GetRequiredService<IIdentityProviderStore>() is not NopIdentityProviderStore)
            {
                if (!licenseValidator.ValidateDynamicProviders())
                {
                    IdentityServerLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Dynamic Identity Providers feature.");
                }
            }

            // Try to get the SAML metadata endpoint. If it exists, then we need to validate the license for SAML.
            if (serviceProvider.GetService<MetadataEndpoint>() != null)
            {
                if (!licenseValidator.ValidateSamlIdp())
                {
                    IdentityServerLicenseValidator.ThrowInvalidLicenseException("Your license does not include the SAML 2.0 Identity Provider feature.");
                }
            }

            TestService(serviceProvider, typeof(IPersistedGrantStore), logger, "No storage mechanism for grants specified. Use the 'AddInMemoryPersistedGrants' extension method to register a development version.");
            TestService(serviceProvider, typeof(IClientStore), logger, "No storage mechanism for clients specified. Use the 'AddInMemoryClients' extension method to register a development version.");
            TestService(serviceProvider, typeof(IResourceStore), logger, "No storage mechanism for resources specified. Use the 'AddInMemoryIdentityResources' or 'AddInMemoryApiResources' extension method to register a development version.");

            var persistedGrants = serviceProvider.GetRequiredService(typeof(IPersistedGrantStore));
            if (persistedGrants.GetType().FullName == typeof(InMemoryPersistedGrantStore).FullName)
            {
                logger.LogInformation("You are using the in-memory version of the persisted grant store. This will store consent decisions, authorization codes, refresh and reference tokens in memory only. If you are using any of those features in production, you want to switch to a different store implementation.");
            }

            ValidateOptions(options, logger);

            ValidateAsync(serviceProvider, logger).GetAwaiter().GetResult();
        }
    }

    private static async Task ValidateAsync(IServiceProvider services, ILogger logger)
    {
        var options = services.GetRequiredService<IdentityServerOptions>();
        var schemes = services.GetRequiredService<IAuthenticationSchemeProvider>();


        if (await schemes.GetDefaultAuthenticateSchemeAsync() == null && options.Authentication.CookieAuthenticationScheme == null)
        {
            logger.LogWarning("No authentication scheme has been set. Setting either a default authentication scheme or a CookieAuthenticationScheme on IdentityServerOptions is required.");
        }
        else
        {
            AuthenticationScheme? authenticationScheme;

            if (options.Authentication.CookieAuthenticationScheme != null)
            {
                authenticationScheme = await schemes.GetSchemeAsync(options.Authentication.CookieAuthenticationScheme);
                if (authenticationScheme != null)
                {
                    logger.LogInformation("Using explicitly configured authentication scheme {scheme} for IdentityServer", options.Authentication.CookieAuthenticationScheme);
                }
            }
            else
            {
                authenticationScheme = await schemes.GetDefaultAuthenticateSchemeAsync();
                if (authenticationScheme != null)
                {
                    logger.LogInformation("Using the default authentication scheme {scheme} for IdentityServer", authenticationScheme.Name);
                }
            }

            if (authenticationScheme == null)
            {
                throw new Exception("Could not locate an authentication scheme for your host. Please configure a default, or set the IdentityServerOptions.Authentication.CookieAuthenticationScheme.");
            }

            if (!typeof(IAuthenticationSignInHandler).IsAssignableFrom(authenticationScheme.HandlerType))
            {
                logger.LogInformation("Authentication scheme {scheme} is configured for IdentityServer, but it is not a scheme that supports signin (like cookies). If you support interactive logins via the browser, then a cookie-based scheme should be used.", authenticationScheme.Name);
            }

            logger.LogDebug("Using {scheme} as default ASP.NET Core scheme for authentication", (await schemes.GetDefaultAuthenticateSchemeAsync())?.Name);
            logger.LogDebug("Using {scheme} as default ASP.NET Core scheme for sign-in", (await schemes.GetDefaultSignInSchemeAsync())?.Name);
            logger.LogDebug("Using {scheme} as default ASP.NET Core scheme for sign-out", (await schemes.GetDefaultSignOutSchemeAsync())?.Name);
            logger.LogDebug("Using {scheme} as default ASP.NET Core scheme for challenge", (await schemes.GetDefaultChallengeSchemeAsync())?.Name);
            logger.LogDebug("Using {scheme} as default ASP.NET Core scheme for forbid", (await schemes.GetDefaultForbidSchemeAsync())?.Name);
        }
    }

    private static void ValidateOptions(IdentityServerOptions options, ILogger logger)
    {
        if (options.IssuerUri.IsPresent())
        {
            logger.LogDebug("Custom IssuerUri set to {0}", options.IssuerUri);
        }

        // these three are dynamically populated later from the cookie handler options
        //if (options.UserInteraction.LoginUrl.IsMissing()) throw new InvalidOperationException("LoginUrl is not configured");
        //if (options.UserInteraction.LoginReturnUrlParameter.IsMissing()) throw new InvalidOperationException("LoginReturnUrlParameter is not configured");
        //if (options.UserInteraction.LogoutUrl.IsMissing()) throw new InvalidOperationException("LogoutUrl is not configured");

        if (options.UserInteraction.LogoutIdParameter.IsMissing())
        {
            throw new InvalidOperationException("LogoutIdParameter is not configured");
        }

        if (options.UserInteraction.ErrorUrl.IsMissing())
        {
            throw new InvalidOperationException("ErrorUrl is not configured");
        }

        if (options.UserInteraction.ErrorIdParameter.IsMissing())
        {
            throw new InvalidOperationException("ErrorIdParameter is not configured");
        }

        if (options.UserInteraction.ConsentUrl.IsMissing())
        {
            throw new InvalidOperationException("ConsentUrl is not configured");
        }

        if (options.UserInteraction.ConsentReturnUrlParameter.IsMissing())
        {
            throw new InvalidOperationException("ConsentReturnUrlParameter is not configured");
        }

        if (options.UserInteraction.CustomRedirectReturnUrlParameter.IsMissing())
        {
            throw new InvalidOperationException("CustomRedirectReturnUrlParameter is not configured");
        }

        if (options.UserInteraction.CreateAccountUrl.IsPresent())
        {
            if (options.UserInteraction.CreateAccountReturnUrlParameter.IsMissing())
            {
                throw new InvalidOperationException("CreateAccountReturnUrlParameter is not configured");
            }
            // if CreateAccountUrl is set, then we internally add to the collection of what we support
            options.UserInteraction.PromptValuesSupported.Add(OidcConstants.PromptModes.Create);
        }

        if (options.Authentication.CheckSessionCookieName.IsMissing())
        {
            throw new InvalidOperationException("CheckSessionCookieName is not configured");
        }

        if (options.Cors.CorsPolicyName.IsMissing())
        {
            throw new InvalidOperationException("CorsPolicyName is not configured");
        }

        if (options.Discovery.DynamicClientRegistration.RegistrationEndpointMode == RegistrationEndpointMode.Static
            && options.Discovery.DynamicClientRegistration.StaticRegistrationEndpoint == null)
        {
            throw new InvalidOperationException("DynamicClientRegistration.CustomRegistrationEndpoint must be set when using static registration endpoint type.");
        }
    }

    internal static object? TestService(IServiceProvider serviceProvider, Type service, ILogger logger, string? message = null, bool doThrow = true)
    {
        var appService = serviceProvider.GetService(service);

        if (appService == null)
        {
            var error = message ?? $"Required service {service.FullName} is not registered in the DI container. Aborting startup";

#pragma warning disable CA2254 // This message may not be structured, and the value is also used below making conditionals awkward
            logger.LogCritical(error);
#pragma warning restore CA2254

            if (doThrow)
            {
                throw new InvalidOperationException(error);
            }
        }

        return appService;
    }
}
