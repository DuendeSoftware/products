// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.EntityFrameworkCore;

namespace Duende.IdentityServer.EntityFramework.Options;

/// <summary>
/// Options for configuring the configuration context.
/// </summary>
public class ConfigurationStoreOptions
{
    /// <summary>
    /// Callback to configure the EF DbContext.
    /// </summary>
    /// <value>
    /// The configure database context.
    /// </value>
    public Action<DbContextOptionsBuilder>? ConfigureDbContext { get; set; }

    /// <summary>
    /// Callback in DI resolve the EF DbContextOptions. If set, ConfigureDbContext will not be used.
    /// </summary>
    /// <value>
    /// The configure database context.
    /// </value>
    public Action<IServiceProvider, DbContextOptionsBuilder>? ResolveDbContextOptions { get; set; }

    /// <summary>
    /// Gets or sets the default schema.
    /// </summary>
    /// <value>
    /// The default schema.
    /// </value>
    public string? DefaultSchema { get; set; }

    /// <summary>
    /// Gets or sets the identity resource table configuration.
    /// </summary>
    /// <value>
    /// The identity resource.
    /// </value>
    public TableConfiguration IdentityResource { get; set; } = new TableConfiguration("IdentityResources");

    /// <summary>
    /// Gets or sets the identity claim table configuration.
    /// </summary>
    /// <value>
    /// The identity claim.
    /// </value>
    public TableConfiguration IdentityResourceClaim { get; set; } =
        new TableConfiguration("IdentityResourceClaims");

    /// <summary>
    /// Gets or sets the identity resource property table configuration.
    /// </summary>
    /// <value>
    /// The client property.
    /// </value>
    public TableConfiguration IdentityResourceProperty { get; set; } =
        new TableConfiguration("IdentityResourceProperties");

    /// <summary>
    /// Gets or sets the API resource table configuration.
    /// </summary>
    /// <value>
    /// The API resource.
    /// </value>
    public TableConfiguration ApiResource { get; set; } = new TableConfiguration("ApiResources");

    /// <summary>
    /// Gets or sets the API secret table configuration.
    /// </summary>
    /// <value>
    /// The API secret.
    /// </value>
    public TableConfiguration ApiResourceSecret { get; set; } = new TableConfiguration("ApiResourceSecrets");

    /// <summary>
    /// Gets or sets the API scope table configuration.
    /// </summary>
    /// <value>
    /// The API scope.
    /// </value>
    public TableConfiguration ApiResourceScope { get; set; } = new TableConfiguration("ApiResourceScopes");

    /// <summary>
    /// Gets or sets the API claim table configuration.
    /// </summary>
    /// <value>
    /// The API claim.
    /// </value>
    public TableConfiguration ApiResourceClaim { get; set; } = new TableConfiguration("ApiResourceClaims");

    /// <summary>
    /// Gets or sets the API resource property table configuration.
    /// </summary>
    /// <value>
    /// The client property.
    /// </value>
    public TableConfiguration ApiResourceProperty { get; set; } = new TableConfiguration("ApiResourceProperties");

    /// <summary>
    /// Gets or sets the client table configuration.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    public TableConfiguration Client { get; set; } = new TableConfiguration("Clients");

    /// <summary>
    /// Gets or sets the type of the client grant table configuration.
    /// </summary>
    /// <value>
    /// The type of the client grant.
    /// </value>
    public TableConfiguration ClientGrantType { get; set; } = new TableConfiguration("ClientGrantTypes");

    /// <summary>
    /// Gets or sets the client redirect URI table configuration.
    /// </summary>
    /// <value>
    /// The client redirect URI.
    /// </value>
    public TableConfiguration ClientRedirectUri { get; set; } = new TableConfiguration("ClientRedirectUris");

    /// <summary>
    /// Gets or sets the client post logout redirect URI table configuration.
    /// </summary>
    /// <value>
    /// The client post logout redirect URI.
    /// </value>
    public TableConfiguration ClientPostLogoutRedirectUri { get; set; } =
        new TableConfiguration("ClientPostLogoutRedirectUris");

    /// <summary>
    /// Gets or sets the client scopes table configuration.
    /// </summary>
    /// <value>
    /// The client scopes.
    /// </value>
    public TableConfiguration ClientScopes { get; set; } = new TableConfiguration("ClientScopes");

    /// <summary>
    /// Gets or sets the client secret table configuration.
    /// </summary>
    /// <value>
    /// The client secret.
    /// </value>
    public TableConfiguration ClientSecret { get; set; } = new TableConfiguration("ClientSecrets");

    /// <summary>
    /// Gets or sets the client claim table configuration.
    /// </summary>
    /// <value>
    /// The client claim.
    /// </value>
    public TableConfiguration ClientClaim { get; set; } = new TableConfiguration("ClientClaims");

    /// <summary>
    /// Gets or sets the client IdP restriction table configuration.
    /// </summary>
    /// <value>
    /// The client IdP restriction.
    /// </value>
    public TableConfiguration ClientIdPRestriction { get; set; } = new TableConfiguration("ClientIdPRestrictions");

    /// <summary>
    /// Gets or sets the client cors origin table configuration.
    /// </summary>
    /// <value>
    /// The client cors origin.
    /// </value>
    public TableConfiguration ClientCorsOrigin { get; set; } = new TableConfiguration("ClientCorsOrigins");

    /// <summary>
    /// Gets or sets the client property table configuration.
    /// </summary>
    /// <value>
    /// The client property.
    /// </value>
    public TableConfiguration ClientProperty { get; set; } = new TableConfiguration("ClientProperties");

    /// <summary>
    /// Gets or sets the scope table configuration.
    /// </summary>
    /// <value>
    /// The API resource.
    /// </value>
    public TableConfiguration ApiScope { get; set; } = new TableConfiguration("ApiScopes");

    /// <summary>
    /// Gets or sets the scope claim table configuration.
    /// </summary>
    /// <value>
    /// The API scope claim.
    /// </value>
    public TableConfiguration ApiScopeClaim { get; set; } = new TableConfiguration("ApiScopeClaims");

    /// <summary>
    /// Gets or sets the API resource property table configuration.
    /// </summary>
    /// <value>
    /// The client property.
    /// </value>
    public TableConfiguration ApiScopeProperty { get; set; } = new TableConfiguration("ApiScopeProperties");

    /// <summary>
    /// Gets or sets the identity providers table configuration.
    /// </summary>
    public TableConfiguration IdentityProvider { get; set; } = new TableConfiguration("IdentityProviders");

    /// <summary>
    /// Gets or sets the SAML service providers table configuration.
    /// </summary>
    public TableConfiguration SamlServiceProvider { get; set; } = new TableConfiguration("SamlServiceProviders");

    /// <summary>
    /// Gets or sets the SAML service provider assertion consumer services table configuration.
    /// </summary>
    public TableConfiguration SamlServiceProviderAssertionConsumerService { get; set; } =
        new TableConfiguration("SamlServiceProviderAssertionConsumerServices");

    /// <summary>
    /// Gets or sets the SAML service provider signing certificates table configuration.
    /// </summary>
    public TableConfiguration SamlServiceProviderSigningCertificate { get; set; } =
        new TableConfiguration("SamlServiceProviderSigningCertificates");

    /// <summary>
    /// Gets or sets the SAML service provider encryption certificates table configuration.
    /// </summary>
    public TableConfiguration SamlServiceProviderEncryptionCertificate { get; set; } =
        new TableConfiguration("SamlServiceProviderEncryptionCertificates");

    /// <summary>
    /// Gets or sets the SAML service provider claim mappings table configuration.
    /// </summary>
    public TableConfiguration SamlServiceProviderClaimMapping { get; set; } =
        new TableConfiguration("SamlServiceProviderClaimMappings");

    /// <summary>
    /// Gets or set if EF DbContext pooling is enabled.
    /// </summary>
    public bool EnablePooling { get; set; }

    /// <summary>
    /// Gets or set the pool size to use when DbContext pooling is enabled. If not set, the EF default is used.
    /// </summary>
    public int? PoolSize { get; set; }
}
