// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff;

/// <summary>
/// Extension methods for the BFF DI services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Duende.BFF services to DI
    /// </summary>
    /// <returns></returns>
    public static IBffBuilder AddBff(this IServiceCollection services, Action<BffOptions>? configureAction = null) => new BffBuilder(services)
            .AddBffServices(configureAction)
            .AddDynamicFrontends();

    /// <summary>
    /// Encapsulates DI options for Duende.BFF
    /// </summary>
    class BffBuilder : IBffBuilder
    {
        /// <summary>
        /// Encapsulates DI options for Duende.BFF
        /// </summary>
        internal BffBuilder(IServiceCollection services) => Services = services;

        private IConfiguration? _loadedConfiguration;

        private readonly List<LoadPluginConfiguration> _pluginConfigurationLoaders = [];

        /// <summary>
        /// Hook for a plugin to register itself for configuration loading.
        /// </summary>
        /// <param name="loadPluginConfiguration"></param>
        void IBffBuilder.RegisterConfigurationLoader(LoadPluginConfiguration loadPluginConfiguration)
        {
            if (_loadedConfiguration == null)
            {
                // If the configuration is not yet loaded, we store the loader for later execution
                _pluginConfigurationLoaders.Add(loadPluginConfiguration);
            }
            else
            {
                // Configuration is already loaded, so we execute the loader immediately
                loadPluginConfiguration(Services, _loadedConfiguration);
            }
        }

        /// <summary>
        /// The service collection
        /// </summary>
        public IServiceCollection Services { get; }

        public IBffBuilder LoadConfiguration(IConfiguration section)
        {
            if (_loadedConfiguration != null)
            {
                throw new InvalidOperationException("Already loaded configuration");
            }

            _loadedConfiguration = section;

            Services.Configure<BffConfiguration>(section);

            // Trigger all configuration loaders from plugins
            foreach (var configLoader in _pluginConfigurationLoaders)
            {
                configLoader(Services, section);
            }
            // We no longer need them. 
            _pluginConfigurationLoaders.Clear();

            return this;
        }

    }

}
