// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Scenarios;
using Duende.IdentityServer.UI.DevPortal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Interaction.Infrastructure;

public delegate WebApplication BuildWebApp(WebApplicationBuilder builder);

/// <summary>
/// Extension methods for registering scenarios and inline WebApplications as Aspire resources.
/// </summary>
public static class InlineWebAppExtensions
{

    /// <summary>
    /// Adds an <see cref="IScenario"/> as a custom Aspire resource with Start/Stop commands.
    /// The scenario starts in NotStarted state — use the dashboard Start command to launch it.
    /// </summary>
    public static IResourceBuilder<ScenarioResource> AddScenario(
        this IDistributedApplicationBuilder builder,
        IScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(scenario);

        var resource = new ScenarioResource(scenario);

        var resourceBuilder = builder.AddResource(resource)
            .WithHttpEndpoint(name: ScenarioResource.HttpEndpointName)
            .ExcludeFromManifest();

        // Set initial state to NotStarted
        builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(async (evt, ct) =>
        {
            var notificationService = evt.Services.GetRequiredService<ResourceNotificationService>();
            await notificationService.PublishUpdateAsync(resource,
                s => s with { State = KnownResourceStates.NotStarted });
        });

        // Start command
        resourceBuilder.WithCommand(
            KnownResourceCommands.StartCommand,
            "Start",
            async context => await StartScenario(resource, builder, context),
            new CommandOptions
            {
                IconName = "Play",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                UpdateState = ctx =>
                    ctx.ResourceSnapshot.State?.Text == KnownResourceStates.NotStarted ||
                    ctx.ResourceSnapshot.State?.Text == KnownResourceStates.Exited ||
                    ctx.ResourceSnapshot.State?.Text == KnownResourceStates.FailedToStart
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Hidden
            });

        // Stop command

        resourceBuilder.WithCommand(
            KnownResourceCommands.StopCommand,
            "Stop",
            async context => await StopScenario(resource, context),
            new CommandOptions
            {
                IconName = "Stop",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                UpdateState = ctx =>
                    ctx.ResourceSnapshot.State?.Text == KnownResourceStates.Running
                        ? ResourceCommandState.Enabled
                        : ResourceCommandState.Hidden
            });

        foreach (var command in scenario.GetCommands())
        {
            resourceBuilder.WithCommand(command.Name, command.Name, async context =>
                {
                    if (!resource.IsRunning)
                    {
                        return new ExecuteCommandResult { Success = false, ErrorMessage = "Scenario is not running." };
                    }

                    // Ensure the CommandHost is started
                    var commandHost = await CommandHost.GetOrCreateCommandHostAsync(builder, context);
                    return await commandHost.ExecuteAsync(command.Name, command.Execute);
                },
                new CommandOptions
                {
                    IconName = command.Icon,
                    IconVariant = IconVariant.Regular,
                    IsHighlighted = true,
                    UpdateState = ctx =>
                        ctx.ResourceSnapshot.State?.Text == KnownResourceStates.Running
                            ? ResourceCommandState.Enabled
                            : ResourceCommandState.Hidden
                });
        }

        return resourceBuilder;
    }



    // --- Scenario lifecycle ---

    private static async Task<ExecuteCommandResult> StartScenario(
        ScenarioResource resource,
        IDistributedApplicationBuilder appBuilder,
        ExecuteCommandContext context)
    {
        var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
        var loggerService = context.ServiceProvider.GetRequiredService<ResourceLoggerService>();
        var resourceLogger = loggerService.GetLogger(resource);

        var otlpEndpoint = appBuilder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"];
        var apiKey = appBuilder.Configuration["AppHost:OtlpApiKey"];
        var headers = !string.IsNullOrWhiteSpace(apiKey) ? $"x-otlp-api-key={apiKey}" : null;

        try
        {
            await notificationService.PublishUpdateAsync(resource,
                s => s with { State = KnownResourceStates.Starting });

            var configurator = new ScenarioConfigurator(resourceLogger, otlpEndpoint, headers);
            await resource.Scenario.StartAsync(configurator, context.CancellationToken);
            resource.IsRunning = true;

            resourceLogger.LogInformation("Scenario '{Name}' started", resource.Scenario.Name);

            // Create and start the DevPortal for every scenario
            var htmlContent = LoadReadmeForScenario(resource.Scenario);
            var devPortal = new DevPortalTestHost(configurator, htmlContent, resource.Scenario.Links);
            await devPortal.StartAsync(context.CancellationToken);
            resource.DevPortal = devPortal;

            // Always publish only the portal link as the URL in the Aspire dashboard
            var portalLink = devPortal.Link;
            var urls = new[] { new UrlSnapshot(portalLink.Label, portalLink.Url.ToString(), IsInternal: false) };

            await notificationService.PublishUpdateAsync(resource,
                s => s with
                {
                    State = KnownResourceStates.Running,
                    StartTimeStamp = DateTime.UtcNow,
                    Urls = [.. urls]
                });

            return new ExecuteCommandResult { Success = true };
        }
        catch (Exception ex)
        {
            resourceLogger.LogError(ex, "Failed to start scenario '{Name}'", resource.Scenario.Name);

            await notificationService.PublishUpdateAsync(resource,
                s => s with { State = KnownResourceStates.FailedToStart });

            return new ExecuteCommandResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<ExecuteCommandResult> StopScenario(
        ScenarioResource resource,
        ExecuteCommandContext context)
    {
        var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
        var loggerService = context.ServiceProvider.GetRequiredService<ResourceLoggerService>();
        var resourceLogger = loggerService.GetLogger(resource);

        try
        {
            if (resource.DevPortal != null)
            {
                await resource.DevPortal.DisposeAsync();
                resource.DevPortal = null;
            }

            await resource.Scenario.StopAsync(context.CancellationToken);
            resource.IsRunning = false;

            resourceLogger.LogInformation("Scenario '{Name}' stopped", resource.Scenario.Name);

            await notificationService.PublishUpdateAsync(resource,
                s => s with
                {
                    State = KnownResourceStates.Exited,
                    Urls = []
                });

            return new ExecuteCommandResult { Success = true };
        }
        catch (Exception ex)
        {
            resourceLogger.LogError(ex, "Failed to stop scenario '{Name}'", resource.Scenario.Name);
            return new ExecuteCommandResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static string LoadReadmeForScenario(IScenario scenario)
    {
        var type = scenario.GetType();
        var assembly = type.Assembly;

        // The root namespace is "Duende.IdentityServer.Interaction"
        // Type namespace might be "Duende.IdentityServer.Interaction.Scenarios.MvcCode"
        // Resource name: "Duende.IdentityServer.Interaction.Scenarios.MvcCode.README.html"
        var ns = type.Namespace ?? "";
        var resourceName = $"{ns}.README.html";

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return $"<h1>{scenario.Name}</h1>\n<p>{scenario.Description}</p>";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // --- Inline app lifecycle (single WebApplication) ---

    private static async Task<ExecuteCommandResult> StartInlineApp(
        InlineWebAppResource resource,
        string name,
        IDistributedApplicationBuilder appBuilder,
        ExecuteCommandContext context)
    {
        var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
        var loggerService = context.ServiceProvider.GetRequiredService<ResourceLoggerService>();
        var resourceLogger = loggerService.GetLogger(resource);

        var otlpEndpoint = appBuilder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"];
        var apiKey = appBuilder.Configuration["AppHost:OtlpApiKey"];
        var headers = !string.IsNullOrWhiteSpace(apiKey) ? $"x-otlp-api-key={apiKey}" : null;

        try
        {
            await notificationService.PublishUpdateAsync(resource,
                s => s with { State = KnownResourceStates.Starting });

            var configurator = new ScenarioConfigurator(resourceLogger, otlpEndpoint, headers);
            var webBuilder = configurator.CreateBuilder(name);

            var app = resource.Factory(webBuilder);
            await app.StartAsync(context.CancellationToken);
            resource.App = app;

            var urls = string.Join(", ", app.Urls);
            resourceLogger.LogInformation("Inline web app '{Name}' started at {Urls}", name, urls);

            await notificationService.PublishUpdateAsync(resource,
                s => s with
                {
                    State = KnownResourceStates.Running,
                    StartTimeStamp = DateTime.UtcNow,
                    Urls = [.. app.Urls.Select(u => new UrlSnapshot(u, u, IsInternal: false))]
                });

            return new ExecuteCommandResult { Success = true };
        }
        catch (Exception ex)
        {
            resourceLogger.LogError(ex, "Failed to start inline web app '{Name}'", name);

            await notificationService.PublishUpdateAsync(resource,
                s => s with { State = KnownResourceStates.FailedToStart });

            return new ExecuteCommandResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<ExecuteCommandResult> StopInlineApp(
        InlineWebAppResource resource,
        string name,
        ExecuteCommandContext context)
    {
        var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
        var loggerService = context.ServiceProvider.GetRequiredService<ResourceLoggerService>();
        var resourceLogger = loggerService.GetLogger(resource);

        try
        {
            if (resource.App != null)
            {
                await resource.App.StopAsync(context.CancellationToken);
                await resource.App.DisposeAsync();
                resource.App = null;
            }

            resourceLogger.LogInformation("Inline web app '{Name}' stopped", name);

            await notificationService.PublishUpdateAsync(resource,
                s => s with
                {
                    State = KnownResourceStates.Exited,
                    Urls = []
                });

            return new ExecuteCommandResult { Success = true };
        }
        catch (Exception ex)
        {
            resourceLogger.LogError(ex, "Failed to stop inline web app '{Name}'", name);
            return new ExecuteCommandResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
