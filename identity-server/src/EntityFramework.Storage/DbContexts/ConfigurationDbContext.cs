// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Extensions;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.DbContexts;

/// <summary>
/// DbContext for the IdentityServer configuration data.
/// </summary>
/// <seealso cref="Microsoft.EntityFrameworkCore.DbContext" />
/// <seealso cref="IConfigurationDbContext" />
public class ConfigurationDbContext : ConfigurationDbContext<ConfigurationDbContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationDbContext"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">storeOptions</exception>
    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options)
        : base(options)
    {
    }
}

/// <summary>
/// DbContext for the IdentityServer configuration data.
/// </summary>
/// <seealso cref="Microsoft.EntityFrameworkCore.DbContext" />
/// <seealso cref="IConfigurationDbContext" />
public class ConfigurationDbContext<TContext> : DbContext, IConfigurationDbContext
    where TContext : DbContext, IConfigurationDbContext
{
    /// <summary>
    /// The store options.
    /// </summary>
    public ConfigurationStoreOptions StoreOptions { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationDbContext"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">storeOptions</exception>
    public ConfigurationDbContext(DbContextOptions<TContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the clients.
    /// </summary>
    /// <value>
    /// The clients.
    /// </value>
    public DbSet<Client> Clients { get; set; }

    /// <summary>
    /// Gets or sets the clients' CORS origins.
    /// </summary>
    /// <value>
    /// The clients CORS origins.
    /// </value>
    public DbSet<ClientCorsOrigin> ClientCorsOrigins { get; set; }

    /// <summary>
    /// Gets or sets the identity resources.
    /// </summary>
    /// <value>
    /// The identity resources.
    /// </value>
    public DbSet<IdentityResource> IdentityResources { get; set; }

    /// <summary>
    /// Gets or sets the API resources.
    /// </summary>
    /// <value>
    /// The API resources.
    /// </value>
    public DbSet<ApiResource> ApiResources { get; set; }

    /// <summary>
    /// Gets or sets the API scopes.
    /// </summary>
    /// <value>
    /// The API resources.
    /// </value>
    public DbSet<ApiScope> ApiScopes { get; set; }

    /// <summary>
    /// Gets or sets the identity providers.
    /// </summary>
    /// <value>
    /// The identity providers.
    /// </value>
    public DbSet<IdentityProvider> IdentityProviders { get; set; }

    /// <summary>
    /// Gets or sets the SAML service providers.
    /// </summary>
    /// <value>
    /// The SAML service providers.
    /// </value>
    public DbSet<SamlServiceProvider> SamlServiceProviders { get; set; }

    /// <summary>
    /// Override this method to further configure the model that was discovered by convention from the entity types
    /// exposed in <see cref="Microsoft.EntityFrameworkCore.DbSet{T}" /> properties on your derived context. The resulting model may be cached
    /// and re-used for subsequent instances of your derived context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context. Databases (and other extensions) typically
    /// define extension methods on this object that allow you to configure aspects of the model that are specific
    /// to a given database.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <remarks>
    /// If a model is explicitly set on the options for this context (via <see cref="Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.UseModel(Microsoft.EntityFrameworkCore.Metadata.IModel)" />)
    /// then this method will not be run.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (StoreOptions is null)
        {
            StoreOptions = this.GetService<ConfigurationStoreOptions>();

            if (StoreOptions is null)
            {
                throw new ArgumentNullException(nameof(StoreOptions), "ConfigurationStoreOptions must be configured in the DI system.");
            }
        }

        modelBuilder.ConfigureClientContext(StoreOptions);
        modelBuilder.ConfigureResourcesContext(StoreOptions);
        modelBuilder.ConfigureIdentityProviderContext(StoreOptions);
        modelBuilder.ConfigureSamlServiceProviderContext(StoreOptions);

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (!optionsBuilder.Options.IsFrozen)
        {
            optionsBuilder.ConfigureWarnings(w => w.Ignore(new EventId[] { RelationalEventId.MultipleCollectionIncludeWarning }));
        }
    }
}
