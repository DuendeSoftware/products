// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;

/// <summary>
/// Generalized test that discovers and runs all commands across all console-based scenarios.
/// Each scenario exposes commands via <see cref="IScenario.GetCommands()"/>. This test
/// starts each scenario, executes each command, and asserts success.
/// </summary>
public sealed class ConsoleFlowTests
{
    private static readonly Type[] ConsoleScenarioTypes =
    [
        typeof(ConsoleClientCredentials),
        typeof(ClientCredentialsDPoP),
        typeof(ResourceOwnerFlow),
        typeof(PrivateKeyJwt),
        typeof(TokenIntrospection)
    ];

    public static IEnumerable<object[]> AllCommands()
    {
        foreach (var scenarioType in ConsoleScenarioTypes)
        {
            var instance = (IScenario)Activator.CreateInstance(scenarioType)!;
            foreach (var command in instance.GetCommands())
            {
                yield return [scenarioType.Name, command.Name];
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllCommands))]
    public async Task Command_executes_successfully(string scenarioTypeName, string commandName)
    {
        var scenarioType = ConsoleScenarioTypes.First(t => t.Name == scenarioTypeName);
        var scenario = (IScenario)Activator.CreateInstance(scenarioType)!;

        var configurator = new MinimalConfigurator();
        await scenario.StartAsync(configurator, CancellationToken.None);

        try
        {
            var command = scenario.GetCommands().First(c => c.Name == commandName);

            var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            var result = await command.Execute(new CommandContext
            {
                HttpClientFactory = factory
            });

            result.Success.ShouldBeTrue(result.ErrorMessage ?? $"Command '{commandName}' failed");
        }
        finally
        {
            await scenario.StopAsync(CancellationToken.None);
        }
    }

    private sealed class MinimalConfigurator : IScenarioConfigurator
    {
        public WebApplicationBuilder CreateBuilder(string serviceName)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Error);
            builder.WebHost.UseUrls("https://127.0.0.1:0");
            return builder;
        }
    }
}
