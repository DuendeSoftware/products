// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff;
public sealed class BffApplicationBuilder : IBffBuilder
{
    internal WebApplicationBuilder Builder { get; }
    public IWebHostBuilder WebHost => Builder.WebHost;
    private IBffBuilder Bff => this;
    public IServiceCollection Services => Builder.Services;

    IConfiguration? IBffBuilder.LoadedConfiguration { get; set; }
    /// <summary>
    /// Hook for a plugin to register itself for configuration loading.
    /// </summary>
    /// <param name="loadPluginConfiguration"></param>
    void IBffBuilder.RegisterConfigurationLoader(LoadPluginConfiguration loadPluginConfiguration)
    {
        if (Bff.LoadedConfiguration == null)
        {
            // If the configuration is not yet loaded, we store the loader for later execution
            PluginConfigurationLoaders.Add(loadPluginConfiguration);
        }
        else
        {
            // Configuration is already loaded, so we execute the loader immediately
            loadPluginConfiguration(Services, Bff.LoadedConfiguration);
        }
    }

    public IBffBuilder LoadConfiguration(IConfiguration section)
    {
        if (Bff.LoadedConfiguration != null)
        {
            throw new InvalidOperationException("Already loaded configuration");
        }

        Bff.LoadedConfiguration = section;

        Bff.Services.Configure<BffConfiguration>(section);

        // Trigger all configuration loaders from plugins
        foreach (var configLoader in PluginConfigurationLoaders)
        {
            configLoader(Services, section);
        }
        // We no longer need them. 
        PluginConfigurationLoaders.Clear();

        return this;
    }

    internal BffApplicationBuilder(string[] args, Action<BffOptions>? options) : this(WebApplication.CreateSlimBuilder(args)) => Bff.AddBffServices(options)
            .AddDynamicFrontends();
    private List<LoadPluginConfiguration> PluginConfigurationLoaders { get; } = [];

    internal BffApplicationBuilder(WebApplicationBuilder builder) => Builder = builder;

    public ConfigurationManager Configuration => Builder.Configuration;

    public static BffApplicationBuilder Create(string[]? args = null, Action<BffOptions>? options = null) => new BffApplicationBuilder(args ?? [], options);

    public BffApplication Build()
    {
        if (Bff.LoadedConfiguration == null)
        {
            LoadConfiguration(Configuration);
        }
        return new BffApplication(Builder.Build());
    }
}
