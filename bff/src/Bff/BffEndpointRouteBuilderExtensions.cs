// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Endpoints;
using Duende.Bff.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LicenseValidator = Duende.Bff.Licensing.LicenseValidator;


namespace Duende.Bff;

/// <summary>
/// Extension methods for the BFF endpoints
/// </summary>
public static class BffEndpointRouteBuilderExtensions
{
    internal static bool LicenseChecked;

    private static Task ProcessWith<T>(HttpContext context)
        where T : IBffEndpoint
    {
        var service = context.RequestServices.GetRequiredService<T>();
        return service.ProcessRequestAsync(context);
    }

    /// <summary>
    /// Adds the BFF management endpoints (login, logout, logout notifications)
    /// </summary>
    /// <param name="endpoints"></param>
    public static void MapBffManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value;
        if (endpoints.AlreadyMappedManagementEndpoint(options.LoginPath, "Login"))
        {
            return;
        }

        endpoints.MapBffManagementLoginEndpoint();
#pragma warning disable CS0618 // Type or member is obsolete
        endpoints.MapBffManagementSilentLoginEndpoints();
#pragma warning restore CS0618 // Type or member is obsolete
        endpoints.MapBffManagementLogoutEndpoint();
        endpoints.MapBffManagementUserEndpoint();
        endpoints.MapBffManagementBackchannelEndpoint();
        endpoints.MapBffDiagnosticsEndpoint();
    }

    /// <summary>
    /// Adds the login BFF management endpoint
    /// </summary>
    /// <param name="endpoints"></param>
    public static void MapBffManagementLoginEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.CheckLicense();

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value;

        endpoints.MapGet(options.LoginPath.Value!, ProcessWith<ILoginEndpoint>)
            .WithMetadata(new BffUiEndpointAttribute())
            .AllowAnonymous();
    }

    internal static bool AlreadyMappedManagementEndpoint(this IEndpointRouteBuilder endpoints, PathString route, string name)
    {
        if (endpoints.DataSources.Any(x =>
                x.Endpoints.OfType<RouteEndpoint>().Any(x => x.RoutePattern.RawText == route.ToString())))
        {
            endpoints.ServiceProvider.GetRequiredService<ILogger<BffBuilder>>().LogWarning("Already mapped {name} endpoint, so the call to MapBffManagementEndpoints will be ignored. If you're using BffOptions.AutomaticallyRegisterBffMiddleware, you don't need to call endpoints.MapBffManagementEndpoints()", name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds the silent login BFF management endpoints
    /// </summary>
    /// <param name="endpoints"></param>
    [Obsolete("The silent login endpoint will be removed in a future version. Silent login is now handled by passing the prompt=none parameter to the login endpoint.")]
    public static void MapBffManagementSilentLoginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.CheckLicense();

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value;

        endpoints.MapGet(options.SilentLoginPath.Value!, ProcessWith<ISilentLoginEndpoint>)
            .WithName("SilentLogin")
            .WithMetadata(new BffUiEndpointAttribute())
            .AllowAnonymous();

        endpoints.MapGet(options.SilentLoginCallbackPath.Value!, ProcessWith<ISilentLoginCallbackEndpoint>)
            .WithMetadata(new BffUiEndpointAttribute())
            .AllowAnonymous();
    }

    /// <summary>
    /// Adds the logout BFF management endpoint
    /// </summary>
    /// <param name="endpoints"></param>
    public static void MapBffManagementLogoutEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.CheckLicense();

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value;

        endpoints.MapGet(options.LogoutPath.Value!, ProcessWith<ILogoutEndpoint>)
            .WithName("Logout")
            .WithMetadata(new BffUiEndpointAttribute())
            .AllowAnonymous();
    }

    /// <summary>
    /// Adds the user BFF management endpoint
    /// </summary>
    /// <param name="endpoints"></param>
    public static void MapBffManagementUserEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.CheckLicense();

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value;

        endpoints.MapGet(options.UserPath.Value!, ProcessWith<IUserEndpoint>)
            .AllowAnonymous()
            .AsBffApiEndpoint();
    }

    /// <summary>
    /// Adds the back channel BFF management endpoint
    /// </summary>
    /// <param name="endpoints"></param>
    public static void MapBffManagementBackchannelEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.CheckLicense();

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value;

        endpoints.MapPost(options.BackChannelLogoutPath.Value!, ProcessWith<IBackchannelLogoutEndpoint>)
            .AllowAnonymous();
    }

    /// <summary>
    /// Adds the diagnostics BFF management endpoint
    /// </summary>
    /// <param name="endpoints"></param>
    public static void MapBffDiagnosticsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.CheckLicense();

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value;

        endpoints.MapGet(options.DiagnosticsPath.Value!, ProcessWith<IDiagnosticsEndpoint>)
            .AllowAnonymous();
    }

    internal static void CheckLicense(this IEndpointRouteBuilder endpoints) => endpoints.ServiceProvider.CheckLicense();

    internal static void CheckLicense(this IServiceProvider serviceProvider)
    {
        if (LicenseChecked == false)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var options = serviceProvider.GetRequiredService<IOptions<BffOptions>>().Value;

            LicenseValidator.Initalize(loggerFactory, options);
            LicenseValidator.ValidateLicense();
        }

        LicenseChecked = true;
    }
}
