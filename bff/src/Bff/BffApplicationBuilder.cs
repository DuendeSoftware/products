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

    private WebApplicationBuilder Builder { get; }
    public IWebHostBuilder WebHost => Builder.WebHost;
    public IServiceCollection Services => Builder.Services;

    private IConfiguration? _loadedConfiguration;
    /// <summary>
    /// Hook for a plugin to register itself for configuration loading.
    /// </summary>
    /// <param name="loadPluginConfiguration"></param>
    void IBffBuilder.RegisterConfigurationLoader(LoadPluginConfiguration loadPluginConfiguration)
    {
        if (_loadedConfiguration == null)
        {
            // If the configuration is not yet loaded, we store the loader for later execution
            PluginConfigurationLoaders.Add(loadPluginConfiguration);
        }
        else
        {
            // Configuration is already loaded, so we execute the loader immediately
            loadPluginConfiguration(Services, _loadedConfiguration);
        }
    }

    public IBffBuilder LoadConfiguration(IConfiguration section)
    {
        if (_loadedConfiguration != null)
        {
            throw new InvalidOperationException("Already loaded configuration");
        }

        _loadedConfiguration = section;

        Services.Configure<BffConfiguration>(section);

        // Trigger all configuration loaders from plugins
        foreach (var configLoader in PluginConfigurationLoaders)
        {
            configLoader(Services, section);
        }
        // We no longer need them. 
        PluginConfigurationLoaders.Clear();

        return this;
    }

    private List<LoadPluginConfiguration> PluginConfigurationLoaders { get; } = [];

    internal BffApplicationBuilder(WebApplicationBuilder builder, Action<BffOptions>? options)
    {
        Builder = builder;
        this.AddBffServices(options)
            .AddDynamicFrontends();
    }

    public ConfigurationManager Configuration => Builder.Configuration;

    public static BffApplicationBuilder Create(string[]? args = null, Action<BffOptions>? options = null)
        => new BffApplicationBuilder(WebApplication.CreateSlimBuilder(args ?? []), options);
    public static BffApplicationBuilder Create(WebApplicationOptions opt, Action<BffOptions>? options = null)
        => new BffApplicationBuilder(WebApplication.CreateSlimBuilder(opt), options);

    public BffApplication Build()
    {
        if (_loadedConfiguration == null)
        {
            LoadConfiguration(Configuration);
        }
        return new BffApplication(Builder.Build());
    }
}
