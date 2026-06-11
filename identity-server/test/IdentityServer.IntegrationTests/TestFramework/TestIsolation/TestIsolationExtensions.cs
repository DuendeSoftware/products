// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;

public static class TestIsolationExtensions
{
    /// <summary>
    /// Registers <see cref="TestIsolationService"/> and snapshots the current
    /// service collection as the base for all per-test containers.
    /// Must be called LAST, after all other service registrations.
    /// </summary>
    public static IServiceCollection AddTestIsolation(this IServiceCollection services)
    {
#pragma warning disable CA2000 // Ownership transferred to DI container via AddSingleton
        var isolationService = new TestIsolationService();
#pragma warning restore CA2000

        // Snapshot services BEFORE adding TestIsolationService itself.
        // TestIsolationService is a shared singleton managed by the global
        // container, it must NOT be cloned into per-test containers because
        // disposing a per-test provider would dispose the shared instance.
        isolationService.SetBaseServices(services);

        _ = services.AddSingleton(isolationService);

        return services;
    }

    /// <summary>
    /// Installs the <see cref="TestIsolationMiddleware"/> dispatcher and captures
    /// the root <see cref="IApplicationBuilder"/> for pipeline branching.
    /// </summary>
    public static IApplicationBuilder UseTestIsolation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var service = app.ApplicationServices
            .GetRequiredService<TestIsolationService>();
        service.SetApplicationBuilder(app);
        _ = app.UseMiddleware<TestIsolationMiddleware>();
        return app;
    }
}
