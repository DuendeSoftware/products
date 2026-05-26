// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Passkeys;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Internal.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal;

internal sealed class UserAuthenticationWebModule : IHttpModule
{
    public static void Register(IServiceCollection services)
    {
        // Passkey endpoint handlers
        _ = services.AddScoped<PasskeyBeginRegistrationEndpoint>();
        _ = services.AddScoped<PasskeyCompleteRegistrationEndpoint>();
        _ = services.AddScoped<PasskeyBeginAuthenticationEndpoint>();
        _ = services.AddScoped<PasskeyCompleteAuthenticationEndpoint>();
    }

    public void MapEndpoints<T>(T app) where T : IEndpointRouteBuilder
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<UserAuthenticationEndpointOptions>>().Value;
        var routes = options.Passkeys;

        var passkeysJs = LoadAndTemplatePasskeysJs(routes);

        var group = app.MapGroup(routes.Route);

        _ = group.MapPost(routes.BeginRegistration, (
                [FromServices] PasskeyBeginRegistrationEndpoint endpoint,
                HttpContext context,
                Ct ct
            ) => endpoint.ProcessAsync(context, ct))
            .RequireAuthorization()
            .WithName("Passkey Register Begin");

        _ = group.MapPost(routes.CompleteRegistration, (
                [FromServices] PasskeyCompleteRegistrationEndpoint endpoint,
                HttpContext context,
                PasskeyCompleteRegistrationRequest request,
                Ct ct
            ) => endpoint.ProcessAsync(context, request, ct))
            .RequireAuthorization()
            .WithName("Passkey Register Complete");

        var isService = app.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (isService.IsService(typeof(ISecondFactorPasskeyAuthenticationResolver)))
        {
            _ = group.MapPost(routes.BeginAuthentication, (
                    [FromServices] PasskeyBeginAuthenticationForSecondFactorEndpoint endpoint,
                    Ct ct
                ) => endpoint.ProcessAsync(ct))
                .WithName("Passkey Authenticate Begin");
        }

        _ = group.MapPost(routes.BeginDiscoverableAuthentication, (
                [FromServices] PasskeyBeginAuthenticationEndpoint endpoint,
                Ct ct
            ) => endpoint.ProcessDiscoverableAsync(ct))
            .WithName("Passkey Authenticate Discoverable Begin");

        _ = group.MapPost(routes.CompleteAuthentication, (
                [FromServices] PasskeyCompleteAuthenticationEndpoint endpoint,
                HttpContext context,
                PasskeyCompleteAuthenticationRequest request,
                Ct ct
            ) => endpoint.ProcessAsync(context, request, ct))
            .WithName("Passkey Authenticate Complete");

        _ = group.MapGet(routes.PasskeysJavaScript,
                () => Results.Content(passkeysJs, "application/javascript"))
            .WithName("Passkeys JavaScript");
    }

    private static string LoadAndTemplatePasskeysJs(PasskeysRouteOptions routes)
    {
        var assembly = typeof(UserAuthenticationWebModule).Assembly;
        const string resourceName = "Duende.UserManagement.Authentication.Passkeys.passkeys.js";

        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{resourceName}' not found. Existing: {string.Join(Environment.NewLine, assembly.GetManifestResourceNames())}");
        using var reader = new StreamReader(stream);
        var template = reader.ReadToEnd();

        return template
            .Replace("{registerBeginUrl}", routes.Route + routes.BeginRegistration, StringComparison.Ordinal)
            .Replace("{registerCompleteUrl}", routes.Route + routes.CompleteRegistration, StringComparison.Ordinal)
            .Replace("{authenticateBeginUrl}", routes.Route + routes.BeginAuthentication, StringComparison.Ordinal)
            .Replace("{authenticateDiscoverableBeginUrl}", routes.Route + routes.BeginDiscoverableAuthentication,
                StringComparison.Ordinal)
            .Replace("{authenticateCompleteUrl}", routes.Route + routes.CompleteAuthentication,
                StringComparison.Ordinal);
    }
}
