// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duende.IdentityServer.EntityFramework.Extensions;

/// <summary>
/// Extension methods to define the database schema for the configuration and operational data stores.
/// </summary>
public static class ModelBuilderExtensions
{
    private static EntityTypeBuilder<TEntity> ToTable<TEntity>(this EntityTypeBuilder<TEntity> entityTypeBuilder, TableConfiguration configuration)
        where TEntity : class
    {
        return string.IsNullOrWhiteSpace(configuration.Schema) ? entityTypeBuilder.ToTable(configuration.Name) : entityTypeBuilder.ToTable(configuration.Name, configuration.Schema);
    }

    /// <summary>
    /// Configures the client context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigureClientContext(this ModelBuilder modelBuilder, ConfigurationStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema)) modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);

        modelBuilder.Entity<Client>(client =>
        {
            client.ToTable(storeOptions.Client);
            client.HasKey(x => x.Id);

            client.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            client.Property(x => x.ProtocolType).HasMaxLength(200).IsRequired();
            client.Property(x => x.ClientName).HasMaxLength(200);
            client.Property(x => x.ClientUri).HasMaxLength(2000);
            client.Property(x => x.LogoUri).HasMaxLength(2000);
            client.Property(x => x.Description).HasMaxLength(1000);
            client.Property(x => x.FrontChannelLogoutUri).HasMaxLength(2000);
            client.Property(x => x.BackChannelLogoutUri).HasMaxLength(2000);
            client.Property(x => x.ClientClaimsPrefix).HasMaxLength(200);
            client.Property(x => x.PairWiseSubjectSalt).HasMaxLength(200);
            client.Property(x => x.UserCodeType).HasMaxLength(100);
            client.Property(x => x.AllowedIdentityTokenSigningAlgorithms).HasMaxLength(100);
            client.Property(x => x.InitiateLoginUri).HasMaxLength(2000);

            client.HasIndex(x => x.ClientId).IsUnique();

            client.HasMany(x => x.AllowedGrantTypes).WithOne(x => x.Client).HasForeignKey(x=>x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.RedirectUris).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.PostLogoutRedirectUris).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.AllowedScopes).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.ClientSecrets).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.Claims).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.IdentityProviderRestrictions).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.AllowedCorsOrigins).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            client.HasMany(x => x.Properties).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClientGrantType>(grantType =>
        {
            grantType.ToTable(storeOptions.ClientGrantType);
            grantType.Property(x => x.GrantType).HasMaxLength(250).IsRequired();

            grantType.HasIndex(x => new { x.ClientId, x.GrantType }).IsUnique();
        });

        modelBuilder.Entity<ClientRedirectUri>(redirectUri =>
        {
            redirectUri.ToTable(storeOptions.ClientRedirectUri);
            redirectUri.Property(x => x.RedirectUri).HasMaxLength(400).IsRequired();

            redirectUri.HasIndex(x => new { x.ClientId, x.RedirectUri }).IsUnique();
        });

        modelBuilder.Entity<ClientPostLogoutRedirectUri>(postLogoutRedirectUri =>
        {
            postLogoutRedirectUri.ToTable(storeOptions.ClientPostLogoutRedirectUri);
            postLogoutRedirectUri.Property(x => x.PostLogoutRedirectUri).HasMaxLength(400).IsRequired();

            postLogoutRedirectUri.HasIndex(x => new { x.ClientId, x.PostLogoutRedirectUri }).IsUnique();
        });

        modelBuilder.Entity<ClientScope>(scope =>
        {
            scope.ToTable(storeOptions.ClientScopes);
            scope.Property(x => x.Scope).HasMaxLength(200).IsRequired();

            scope.HasIndex(x => new { x.ClientId, x.Scope }).IsUnique();
        });

        modelBuilder.Entity<ClientSecret>(secret =>
        {
            secret.ToTable(storeOptions.ClientSecret);
            secret.Property(x => x.Value).HasMaxLength(4000).IsRequired();
            secret.Property(x => x.Type).HasMaxLength(250).IsRequired();
            secret.Property(x => x.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<ClientClaim>(claim =>
        {
            claim.ToTable(storeOptions.ClientClaim);
            claim.Property(x => x.Type).HasMaxLength(250).IsRequired();
            claim.Property(x => x.Value).HasMaxLength(250).IsRequired();

            claim.HasIndex(x => new { x.ClientId, x.Type, x.Value }).IsUnique();
        });

        modelBuilder.Entity<ClientIdPRestriction>(idPRestriction =>
        {
            idPRestriction.ToTable(storeOptions.ClientIdPRestriction);
            idPRestriction.Property(x => x.Provider).HasMaxLength(200).IsRequired();

            idPRestriction.HasIndex(x => new { x.ClientId, x.Provider }).IsUnique();
        });

        modelBuilder.Entity<ClientCorsOrigin>(corsOrigin =>
        {
            corsOrigin.ToTable(storeOptions.ClientCorsOrigin);
            corsOrigin.Property(x => x.Origin).HasMaxLength(150).IsRequired();

            corsOrigin.HasIndex(x => new { x.ClientId, x.Origin }).IsUnique();
        });

        modelBuilder.Entity<ClientProperty>(property =>
        {
            property.ToTable(storeOptions.ClientProperty);
            property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            property.HasIndex(x => new { x.ClientId, x.Key }).IsUnique();
        });
    }

    /// <summary>
    /// Configures the persisted grant context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigurePersistedGrantContext(this ModelBuilder modelBuilder, OperationalStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema)) modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);

        modelBuilder.Entity<PersistedGrant>(grant =>
        {
            grant.ToTable(storeOptions.PersistedGrants);

            grant.Property(x => x.Key).HasMaxLength(200);
            grant.Property(x => x.Type).HasMaxLength(50).IsRequired();
            grant.Property(x => x.SubjectId).HasMaxLength(200);
            grant.Property(x => x.SessionId).HasMaxLength(100);
            grant.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            grant.Property(x => x.Description).HasMaxLength(200);
            grant.Property(x => x.CreationTime).IsRequired();
            // 50000 chosen to be explicit to allow enough size to avoid truncation, yet stay beneath the MySql row size limit of ~65K
            // apparently anything over 4K converts to nvarchar(max) on SqlServer
            grant.Property(x => x.Data).HasMaxLength(50000).IsRequired();

            grant.HasKey(x => x.Id);

            grant.HasIndex(x => x.Key).IsUnique();
            grant.HasIndex(x => new { x.SubjectId, x.ClientId, x.Type });
            grant.HasIndex(x => new { x.SubjectId, x.SessionId, x.Type });
            grant.HasIndex(x => x.Expiration);
            grant.HasIndex(x => x.ConsumedTime);
        });

        modelBuilder.Entity<DeviceFlowCodes>(codes =>
        {
            codes.ToTable(storeOptions.DeviceFlowCodes);

            codes.Property(x => x.DeviceCode).HasMaxLength(200).IsRequired();
            codes.Property(x => x.UserCode).HasMaxLength(200).IsRequired();
            codes.Property(x => x.SubjectId).HasMaxLength(200);
            codes.Property(x => x.SessionId).HasMaxLength(100);
            codes.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            codes.Property(x => x.Description).HasMaxLength(200);
            codes.Property(x => x.CreationTime).IsRequired();
            codes.Property(x => x.Expiration).IsRequired();
            // 50000 chosen to be explicit to allow enough size to avoid truncation, yet stay beneath the MySql row size limit of ~65K
            // apparently anything over 4K converts to nvarchar(max) on SqlServer
            codes.Property(x => x.Data).HasMaxLength(50000).IsRequired();

            codes.HasKey(x => new {x.UserCode});

            codes.HasIndex(x => x.DeviceCode).IsUnique();
            codes.HasIndex(x => x.Expiration);
        });

        modelBuilder.Entity<Key>(entity =>
        {
            entity.ToTable(storeOptions.Keys);

            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Use);
            entity.Property(x => x.Algorithm).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Data).IsRequired();
        });

        modelBuilder.Entity<ServerSideSession>(entity =>
        {
            entity.ToTable(storeOptions.ServerSideSessions);

            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Scheme).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SubjectId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SessionId).HasMaxLength(100);
            entity.Property(x => x.DisplayName).HasMaxLength(100);
            entity.Property(x => x.Data).IsRequired();

            entity.HasIndex(x => x.Expires);
            entity.HasIndex(x => x.SubjectId);
            entity.HasIndex(x => x.SessionId);
            entity.HasIndex(x => x.DisplayName);
        });

        modelBuilder.Entity<PushedAuthorizationRequest>(entity =>
        {
            entity.ToTable(storeOptions.PushedAuthorizationRequests);

            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReferenceValueHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.Parameters).IsRequired();

            entity.HasIndex(x => x.ReferenceValueHash).IsUnique();
            entity.HasIndex(x => x.ExpiresAtUtc);
        });
    }

    /// <summary>
    /// Configures the resources context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigureResourcesContext(this ModelBuilder modelBuilder, ConfigurationStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema)) modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);

        modelBuilder.Entity<IdentityResource>(identityResource =>
        {
            identityResource.ToTable(storeOptions.IdentityResource).HasKey(x => x.Id);

            identityResource.Property(x => x.Name).HasMaxLength(200).IsRequired();
            identityResource.Property(x => x.DisplayName).HasMaxLength(200);
            identityResource.Property(x => x.Description).HasMaxLength(1000);

            identityResource.HasIndex(x => x.Name).IsUnique();

            identityResource.HasMany(x => x.UserClaims).WithOne(x => x.IdentityResource).HasForeignKey(x => x.IdentityResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            identityResource.HasMany(x => x.Properties).WithOne(x => x.IdentityResource).HasForeignKey(x => x.IdentityResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdentityResourceClaim>(claim =>
        {
            claim.ToTable(storeOptions.IdentityResourceClaim).HasKey(x => x.Id);

            claim.Property(x => x.Type).HasMaxLength(200).IsRequired();

            claim.HasIndex(x => new { x.IdentityResourceId, x.Type }).IsUnique();
        });

        modelBuilder.Entity<IdentityResourceProperty>(property =>
        {
            property.ToTable(storeOptions.IdentityResourceProperty);
            property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            property.HasIndex(x => new { x.IdentityResourceId, x.Key }).IsUnique();
        });



        modelBuilder.Entity<ApiResource>(apiResource =>
        {
            apiResource.ToTable(storeOptions.ApiResource).HasKey(x => x.Id);

            apiResource.Property(x => x.Name).HasMaxLength(200).IsRequired();
            apiResource.Property(x => x.DisplayName).HasMaxLength(200);
            apiResource.Property(x => x.Description).HasMaxLength(1000);
            apiResource.Property(x => x.AllowedAccessTokenSigningAlgorithms).HasMaxLength(100);

            apiResource.HasIndex(x => x.Name).IsUnique();

            apiResource.HasMany(x => x.Secrets).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            apiResource.HasMany(x => x.Scopes).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            apiResource.HasMany(x => x.UserClaims).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            apiResource.HasMany(x => x.Properties).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiResourceSecret>(apiSecret =>
        {
            apiSecret.ToTable(storeOptions.ApiResourceSecret).HasKey(x => x.Id);

            apiSecret.Property(x => x.Description).HasMaxLength(1000);
            apiSecret.Property(x => x.Value).HasMaxLength(4000).IsRequired();
            apiSecret.Property(x => x.Type).HasMaxLength(250).IsRequired();
        });

        modelBuilder.Entity<ApiResourceClaim>(apiClaim =>
        {
            apiClaim.ToTable(storeOptions.ApiResourceClaim).HasKey(x => x.Id);

            apiClaim.Property(x => x.Type).HasMaxLength(200).IsRequired();

            apiClaim.HasIndex(x => new { x.ApiResourceId, x.Type }).IsUnique();
        });

        modelBuilder.Entity<ApiResourceScope>(apiScope =>
        {
            apiScope.ToTable(storeOptions.ApiResourceScope).HasKey(x => x.Id);

            apiScope.Property(x => x.Scope).HasMaxLength(200).IsRequired();

            apiScope.HasIndex(x => new { x.ApiResourceId, x.Scope }).IsUnique();
        });

        modelBuilder.Entity<ApiResourceProperty>(property =>
        {
            property.ToTable(storeOptions.ApiResourceProperty);
            property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            property.HasIndex(x => new { x.ApiResourceId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<ApiScope>(scope =>
        {
            scope.ToTable(storeOptions.ApiScope).HasKey(x => x.Id);

            scope.Property(x => x.Name).HasMaxLength(200).IsRequired();
            scope.Property(x => x.DisplayName).HasMaxLength(200);
            scope.Property(x => x.Description).HasMaxLength(1000);

            scope.HasIndex(x => x.Name).IsUnique();

            scope.HasMany(x => x.UserClaims).WithOne(x => x.Scope).HasForeignKey(x => x.ScopeId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ApiScopeClaim>(scopeClaim =>
        {
            scopeClaim.ToTable(storeOptions.ApiScopeClaim).HasKey(x => x.Id);

            scopeClaim.Property(x => x.Type).HasMaxLength(200).IsRequired();

            scopeClaim.HasIndex(x => new { x.ScopeId, x.Type }).IsUnique();
        });
        modelBuilder.Entity<ApiScopeProperty>(property =>
        {
            property.ToTable(storeOptions.ApiScopeProperty).HasKey(x => x.Id);
            property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            property.HasIndex(x => new { x.ScopeId, x.Key }).IsUnique();
        });
    }

    /// <summary>
    /// Configures the identity providers.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigureIdentityProviderContext(this ModelBuilder modelBuilder, ConfigurationStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema)) modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);

        modelBuilder.Entity<IdentityProvider>(entity =>
        {
            entity.ToTable(storeOptions.IdentityProvider).HasKey(x => x.Id);

            entity.Property(x => x.Scheme).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200);

            entity.HasIndex(x => x.Scheme).IsUnique();
        });
    }
}
