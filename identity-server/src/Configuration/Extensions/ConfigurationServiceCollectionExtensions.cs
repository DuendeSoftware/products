// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration.Configuration;
using Duende.IdentityServer.Configuration.RequestProcessing;
using Duende.IdentityServer.Configuration.ResponseGeneration;
using Duende.IdentityServer.Configuration.Validation.DynamicClientRegistration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Builder class for setting up services for IdentityServer.Configuration. 
/// </summary>
public class IdentityServerConfigurationBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="IdentityServerConfigurationBuilder"/> class.
    /// </summary>
    public IdentityServerConfigurationBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }
}

/// <summary>
/// Extension methods for adding IdentityServer.Configuration services.
/// </summary>
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Adds IdentityServer.Configuration services with configuration options
    /// specified by a lambda.
    /// </summary>
    public static IdentityServerConfigurationBuilder AddIdentityServerConfiguration(this IServiceCollection services, Action<IdentityServerConfigurationOptions> setupAction)
    {
        services.Configure(setupAction);
        return AddIdentityServerConfiguration(services);
    }

    /// <summary>
    /// Adds IdentityServer.Configuration services with configuration options
    /// specified by an <see cref="IConfiguration" />.
    /// </summary>
    public static IdentityServerConfigurationBuilder AddIdentityServerConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentityServerConfigurationOptions>(configuration);
        return services.AddIdentityServerConfiguration();
    }

    private static IdentityServerConfigurationBuilder AddIdentityServerConfiguration(this IServiceCollection services)
    {
        var builder = new IdentityServerConfigurationBuilder(services);

        builder.Services.AddTransient<DynamicClientRegistrationEndpoint>();
        builder.Services.AddTransient(
            resolver => resolver.GetRequiredService<IOptionsMonitor<IdentityServerConfigurationOptions>>().CurrentValue);

        builder.Services.TryAddTransient<IDynamicClientRegistrationValidator, DynamicClientRegistrationValidator>();
        builder.Services.TryAddTransient<IDynamicClientRegistrationRequestProcessor, DynamicClientRegistrationRequestProcessor>();
        builder.Services.TryAddTransient<IDynamicClientRegistrationResponseGenerator, DynamicClientRegistrationResponseGenerator>();

        return builder;
    }
}
