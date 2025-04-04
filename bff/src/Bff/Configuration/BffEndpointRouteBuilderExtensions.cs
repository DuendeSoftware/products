// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.EndpointProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LicenseValidator = Duende.Bff.Licensing.LicenseValidator;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for the BFF endpoints
/// </summary>
public static class BffEndpointRouteBuilderExtensions
{
    internal static bool LicenseChecked;

    private static Task ProcessWith<T>(HttpContext context)
        where T : IBffEndpointService
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

        endpoints.MapGet(options.LoginPath.Value!, ProcessWith<ILoginService>)
            .WithMetadata(new BffUiEndpointAttribute())
            .AllowAnonymous();
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

        endpoints.MapGet(options.SilentLoginPath.Value!, ProcessWith<ISilentLoginService>)
            .WithMetadata(new BffUiEndpointAttribute())
            .AllowAnonymous();

        endpoints.MapGet(options.SilentLoginCallbackPath.Value!, ProcessWith<ISilentLoginCallbackService>)
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

        endpoints.MapGet(options.LogoutPath.Value!, ProcessWith<ILogoutService>)
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

        endpoints.MapGet(options.UserPath.Value!, ProcessWith<IUserService>)
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

        endpoints.MapPost(options.BackChannelLogoutPath.Value!, ProcessWith<IBackchannelLogoutService>)
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

        endpoints.MapGet(options.DiagnosticsPath.Value!, ProcessWith<IDiagnosticsService>)
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
