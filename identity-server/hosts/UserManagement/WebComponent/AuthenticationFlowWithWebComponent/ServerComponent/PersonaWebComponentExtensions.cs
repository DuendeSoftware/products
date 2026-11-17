// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// Extension methods for configuring Persona Web Component
/// </summary>
public static class PersonaWebComponentExtensions
{
    /// <summary>
    /// Adds Persona Web Component services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPersonaWebComponent(
        this IServiceCollection services,
        Action<WebComponentOptions>? configure = null)
    {
        var options = new WebComponentOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<CookieOtpStateService>();
        services.AddSingleton<CookieAuthenticationStateService>();
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Maps Persona Web Component endpoints to the application
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="basePath">The base path for authentication endpoints (default: "/api/auth")</param>
    /// <returns>The endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder MapPersonaWebComponent(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/auth")
    {
        var group = endpoints.MapGroup(basePath);

        // GET endpoints
        group.MapGet("/state", AuthenticationStateHandler.GetAuthenticationStateAsync)
             .WithName("GetAuthenticationState");

        group.MapGet("/config", AuthenticationEndpointHandlers.GetAuthConfig)
             .WithName("GetAuthConfig");

        // POST endpoints
        group.MapPost("/otp/send", AuthenticationEndpointHandlers.SendOtpAsync)
             .WithName("SendOtp");

        group.MapPost("/otp/verify", AuthenticationEndpointHandlers.VerifyOtpAsync)
             .WithName("VerifyOtp");

        group.MapPost("/mfa/verify", AuthenticationEndpointHandlers.VerifyMfaAsync)
             .WithName("VerifyMfa");

        // External challenge is a GET that redirects
        group.MapGet("/external/{provider}/challenge", AuthenticationEndpointHandlers.ExternalChallengeAsync)
             .WithName("ExternalChallenge");

        // Logout endpoint
        group.MapGet("/logout", AuthenticationEndpointHandlers.LogoutAsync)
             .WithName("Logout");

        // Cancel authentication flow endpoint
        group.MapPost("/cancel", AuthenticationEndpointHandlers.CancelAuthenticationFlow)
             .WithName("CancelAuthenticationFlow");

        return endpoints;
    }
}
