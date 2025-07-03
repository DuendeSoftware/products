// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.Bff.Builder;

internal class BffApplicationBuilder : IBffApplicationBuilder
{
    IList<IBffPartBuilder> IBffApplicationBuilder.Parts { get; } = new List<IBffPartBuilder>();
    public List<ConfigureWebAppBuilder> WebAppConfigurators { get; } = new List<ConfigureWebAppBuilder>();

    internal BffApplicationBuilder(IHostApplicationBuilder hostBuilder)
    {
        HostBuilder = hostBuilder;
        if (!HostBuilder.Properties.TryAdd(typeof(BffApplicationBuilder), this))
        {
            throw new InvalidOperationException("BffApplicationBuilder is already registered. You can only register it once per application.");
        }
        hostBuilder.Services.AddSingleton<RootContainer>();
    }

    IServiceCollection IBffApplicationBuilder.InternalServices => HostBuilder.Services;
    public IHostApplicationBuilder HostBuilder { get; }

    public IBffApplicationBuilder UsingServiceDefaults(Action<BffApplicationPartType, IHostApplicationBuilder> configure)
    {
        WebAppConfigurators.Add((type, builder) => configure(type, builder));
        return this;
    }

    public IBffApplicationBuilder UsingServiceDefaults(Action<IHostApplicationBuilder> configure)
    {
        WebAppConfigurators.Add((type, builder) => configure(builder));
        return this;
    }
}
