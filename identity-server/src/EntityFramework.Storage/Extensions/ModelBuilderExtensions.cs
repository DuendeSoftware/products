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
        where TEntity : class => string.IsNullOrWhiteSpace(configuration.Schema) ? entityTypeBuilder.ToTable(configuration.Name) : entityTypeBuilder.ToTable(configuration.Name, configuration.Schema);

    /// <summary>
    /// Configures the client context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigureClientContext(this ModelBuilder modelBuilder, ConfigurationStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema))
        {
            _ = modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);
        }

        _ = modelBuilder.Entity<Client>(client =>
        {
            _ = client.ToTable(storeOptions.Client);
            _ = client.HasKey(x => x.Id);

            _ = client.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            _ = client.Property(x => x.ProtocolType).HasMaxLength(200).IsRequired();
            _ = client.Property(x => x.ClientName).HasMaxLength(200);
            _ = client.Property(x => x.ClientUri).HasMaxLength(2000);
            _ = client.Property(x => x.LogoUri).HasMaxLength(2000);
            _ = client.Property(x => x.Description).HasMaxLength(1000);
            _ = client.Property(x => x.FrontChannelLogoutUri).HasMaxLength(2000);
            _ = client.Property(x => x.BackChannelLogoutUri).HasMaxLength(2000);
            _ = client.Property(x => x.ClientClaimsPrefix).HasMaxLength(200);
            _ = client.Property(x => x.PairWiseSubjectSalt).HasMaxLength(200);
            _ = client.Property(x => x.UserCodeType).HasMaxLength(100);
            _ = client.Property(x => x.AllowedIdentityTokenSigningAlgorithms).HasMaxLength(100);
            _ = client.Property(x => x.InitiateLoginUri).HasMaxLength(2000);

            _ = client.HasIndex(x => x.ClientId).IsUnique();

            _ = client.HasMany(x => x.AllowedGrantTypes).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.RedirectUris).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.PostLogoutRedirectUris).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.AllowedScopes).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.ClientSecrets).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.Claims).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.IdentityProviderRestrictions).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.AllowedCorsOrigins).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = client.HasMany(x => x.Properties).WithOne(x => x.Client).HasForeignKey(x => x.ClientId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        _ = modelBuilder.Entity<ClientGrantType>(grantType =>
        {
            _ = grantType.ToTable(storeOptions.ClientGrantType);
            _ = grantType.Property(x => x.GrantType).HasMaxLength(250).IsRequired();

            _ = grantType.HasIndex(x => new { x.ClientId, x.GrantType }).IsUnique();
        });

        _ = modelBuilder.Entity<ClientRedirectUri>(redirectUri =>
        {
            _ = redirectUri.ToTable(storeOptions.ClientRedirectUri);
            _ = redirectUri.Property(x => x.RedirectUri).HasMaxLength(400).IsRequired();

            _ = redirectUri.HasIndex(x => new { x.ClientId, x.RedirectUri }).IsUnique();
        });

        _ = modelBuilder.Entity<ClientPostLogoutRedirectUri>(postLogoutRedirectUri =>
        {
            _ = postLogoutRedirectUri.ToTable(storeOptions.ClientPostLogoutRedirectUri);
            _ = postLogoutRedirectUri.Property(x => x.PostLogoutRedirectUri).HasMaxLength(400).IsRequired();

            _ = postLogoutRedirectUri.HasIndex(x => new { x.ClientId, x.PostLogoutRedirectUri }).IsUnique();
        });

        _ = modelBuilder.Entity<ClientScope>(scope =>
        {
            _ = scope.ToTable(storeOptions.ClientScopes);
            _ = scope.Property(x => x.Scope).HasMaxLength(200).IsRequired();

            _ = scope.HasIndex(x => new { x.ClientId, x.Scope }).IsUnique();
        });

        _ = modelBuilder.Entity<ClientSecret>(secret =>
        {
            _ = secret.ToTable(storeOptions.ClientSecret);
            _ = secret.Property(x => x.Value).HasMaxLength(4000).IsRequired();
            _ = secret.Property(x => x.Type).HasMaxLength(250).IsRequired();
            _ = secret.Property(x => x.Description).HasMaxLength(2000);
        });

        _ = modelBuilder.Entity<ClientClaim>(claim =>
        {
            _ = claim.ToTable(storeOptions.ClientClaim);
            _ = claim.Property(x => x.Type).HasMaxLength(250).IsRequired();
            _ = claim.Property(x => x.Value).HasMaxLength(250).IsRequired();

            _ = claim.HasIndex(x => new { x.ClientId, x.Type, x.Value }).IsUnique();
        });

        _ = modelBuilder.Entity<ClientIdPRestriction>(idPRestriction =>
        {
            _ = idPRestriction.ToTable(storeOptions.ClientIdPRestriction);
            _ = idPRestriction.Property(x => x.Provider).HasMaxLength(200).IsRequired();

            _ = idPRestriction.HasIndex(x => new { x.ClientId, x.Provider }).IsUnique();
        });

        _ = modelBuilder.Entity<ClientCorsOrigin>(corsOrigin =>
        {
            _ = corsOrigin.ToTable(storeOptions.ClientCorsOrigin);
            _ = corsOrigin.Property(x => x.Origin).HasMaxLength(150).IsRequired();

            _ = corsOrigin.HasIndex(x => new { x.ClientId, x.Origin }).IsUnique();
        });

        _ = modelBuilder.Entity<ClientProperty>(property =>
        {
            _ = property.ToTable(storeOptions.ClientProperty);
            _ = property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            _ = property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            _ = property.HasIndex(x => new { x.ClientId, x.Key }).IsUnique();
        });
    }

    /// <summary>
    /// Configures the persisted grant context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigurePersistedGrantContext(this ModelBuilder modelBuilder, OperationalStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema))
        {
            _ = modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);
        }

        _ = modelBuilder.Entity<PersistedGrant>(grant =>
        {
            _ = grant.ToTable(storeOptions.PersistedGrants);

            _ = grant.Property(x => x.Key).HasMaxLength(200);
            _ = grant.Property(x => x.Type).HasMaxLength(50).IsRequired();
            _ = grant.Property(x => x.SubjectId).HasMaxLength(200);
            _ = grant.Property(x => x.SessionId).HasMaxLength(100);
            _ = grant.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            _ = grant.Property(x => x.Description).HasMaxLength(200);
            _ = grant.Property(x => x.CreationTime).IsRequired();
            // 50000 chosen to be explicit to allow enough size to avoid truncation, yet stay beneath the MySql row size limit of ~65K
            // apparently anything over 4K converts to nvarchar(max) on SqlServer
            _ = grant.Property(x => x.Data).HasMaxLength(50000).IsRequired();

            _ = grant.HasKey(x => x.Id);

            _ = grant.HasIndex(x => x.Key).IsUnique();
            _ = grant.HasIndex(x => new { x.SubjectId, x.ClientId, x.Type });
            _ = grant.HasIndex(x => new { x.SubjectId, x.SessionId, x.Type });
            _ = grant.HasIndex(x => x.Expiration);
            _ = grant.HasIndex(x => x.ConsumedTime);
        });

        _ = modelBuilder.Entity<DeviceFlowCodes>(codes =>
        {
            _ = codes.ToTable(storeOptions.DeviceFlowCodes);

            _ = codes.Property(x => x.DeviceCode).HasMaxLength(200).IsRequired();
            _ = codes.Property(x => x.UserCode).HasMaxLength(200).IsRequired();
            _ = codes.Property(x => x.SubjectId).HasMaxLength(200);
            _ = codes.Property(x => x.SessionId).HasMaxLength(100);
            _ = codes.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            _ = codes.Property(x => x.Description).HasMaxLength(200);
            _ = codes.Property(x => x.CreationTime).IsRequired();
            _ = codes.Property(x => x.Expiration).IsRequired();
            // 50000 chosen to be explicit to allow enough size to avoid truncation, yet stay beneath the MySql row size limit of ~65K
            // apparently anything over 4K converts to nvarchar(max) on SqlServer
            _ = codes.Property(x => x.Data).HasMaxLength(50000).IsRequired();

            _ = codes.HasKey(x => new { x.UserCode });

            _ = codes.HasIndex(x => x.DeviceCode).IsUnique();
            _ = codes.HasIndex(x => x.Expiration);
        });

        _ = modelBuilder.Entity<Key>(entity =>
        {
            _ = entity.ToTable(storeOptions.Keys);

            _ = entity.HasKey(x => x.Id);
            _ = entity.HasIndex(x => x.Use);
            _ = entity.Property(x => x.Algorithm).HasMaxLength(100).IsRequired();
            _ = entity.Property(x => x.Data).IsRequired();
        });

        _ = modelBuilder.Entity<ServerSideSession>(entity =>
        {
            _ = entity.ToTable(storeOptions.ServerSideSessions);

            _ = entity.HasKey(x => x.Id);
            _ = entity.HasIndex(x => x.Key).IsUnique();
            _ = entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
            _ = entity.Property(x => x.Scheme).HasMaxLength(100).IsRequired();
            _ = entity.Property(x => x.SubjectId).HasMaxLength(100).IsRequired();
            _ = entity.Property(x => x.SessionId).HasMaxLength(100);
            _ = entity.Property(x => x.DisplayName).HasMaxLength(100);
            _ = entity.Property(x => x.Data).IsRequired();

            _ = entity.HasIndex(x => x.Expires);
            _ = entity.HasIndex(x => x.SubjectId);
            _ = entity.HasIndex(x => x.SessionId);
            _ = entity.HasIndex(x => x.DisplayName);
        });

        _ = modelBuilder.Entity<PushedAuthorizationRequest>(entity =>
        {
            _ = entity.ToTable(storeOptions.PushedAuthorizationRequests);

            _ = entity.HasKey(x => x.Id);
            _ = entity.Property(x => x.ReferenceValueHash).HasMaxLength(64).IsRequired();
            _ = entity.Property(x => x.ExpiresAtUtc).IsRequired();
            _ = entity.Property(x => x.Parameters).IsRequired();

            _ = entity.HasIndex(x => x.ReferenceValueHash).IsUnique();
            _ = entity.HasIndex(x => x.ExpiresAtUtc);
        });
    }

    /// <summary>
    /// Configures the resources context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigureResourcesContext(this ModelBuilder modelBuilder, ConfigurationStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema))
        {
            _ = modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);
        }

        _ = modelBuilder.Entity<IdentityResource>(identityResource =>
        {
            _ = identityResource.ToTable(storeOptions.IdentityResource).HasKey(x => x.Id);

            _ = identityResource.Property(x => x.Name).HasMaxLength(200).IsRequired();
            _ = identityResource.Property(x => x.DisplayName).HasMaxLength(200);
            _ = identityResource.Property(x => x.Description).HasMaxLength(1000);

            _ = identityResource.HasIndex(x => x.Name).IsUnique();

            _ = identityResource.HasMany(x => x.UserClaims).WithOne(x => x.IdentityResource).HasForeignKey(x => x.IdentityResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = identityResource.HasMany(x => x.Properties).WithOne(x => x.IdentityResource).HasForeignKey(x => x.IdentityResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        _ = modelBuilder.Entity<IdentityResourceClaim>(claim =>
        {
            _ = claim.ToTable(storeOptions.IdentityResourceClaim).HasKey(x => x.Id);

            _ = claim.Property(x => x.Type).HasMaxLength(200).IsRequired();

            _ = claim.HasIndex(x => new { x.IdentityResourceId, x.Type }).IsUnique();
        });

        _ = modelBuilder.Entity<IdentityResourceProperty>(property =>
        {
            _ = property.ToTable(storeOptions.IdentityResourceProperty);
            _ = property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            _ = property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            _ = property.HasIndex(x => new { x.IdentityResourceId, x.Key }).IsUnique();
        });



        _ = modelBuilder.Entity<ApiResource>(apiResource =>
        {
            _ = apiResource.ToTable(storeOptions.ApiResource).HasKey(x => x.Id);

            _ = apiResource.Property(x => x.Name).HasMaxLength(200).IsRequired();
            _ = apiResource.Property(x => x.DisplayName).HasMaxLength(200);
            _ = apiResource.Property(x => x.Description).HasMaxLength(1000);
            _ = apiResource.Property(x => x.AllowedAccessTokenSigningAlgorithms).HasMaxLength(100);

            _ = apiResource.HasIndex(x => x.Name).IsUnique();

            _ = apiResource.HasMany(x => x.Secrets).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = apiResource.HasMany(x => x.Scopes).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = apiResource.HasMany(x => x.UserClaims).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            _ = apiResource.HasMany(x => x.Properties).WithOne(x => x.ApiResource).HasForeignKey(x => x.ApiResourceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        _ = modelBuilder.Entity<ApiResourceSecret>(apiSecret =>
        {
            _ = apiSecret.ToTable(storeOptions.ApiResourceSecret).HasKey(x => x.Id);

            _ = apiSecret.Property(x => x.Description).HasMaxLength(1000);
            _ = apiSecret.Property(x => x.Value).HasMaxLength(4000).IsRequired();
            _ = apiSecret.Property(x => x.Type).HasMaxLength(250).IsRequired();
        });

        _ = modelBuilder.Entity<ApiResourceClaim>(apiClaim =>
        {
            _ = apiClaim.ToTable(storeOptions.ApiResourceClaim).HasKey(x => x.Id);

            _ = apiClaim.Property(x => x.Type).HasMaxLength(200).IsRequired();

            _ = apiClaim.HasIndex(x => new { x.ApiResourceId, x.Type }).IsUnique();
        });

        _ = modelBuilder.Entity<ApiResourceScope>(apiScope =>
        {
            _ = apiScope.ToTable(storeOptions.ApiResourceScope).HasKey(x => x.Id);

            _ = apiScope.Property(x => x.Scope).HasMaxLength(200).IsRequired();

            _ = apiScope.HasIndex(x => new { x.ApiResourceId, x.Scope }).IsUnique();
        });

        _ = modelBuilder.Entity<ApiResourceProperty>(property =>
        {
            _ = property.ToTable(storeOptions.ApiResourceProperty);
            _ = property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            _ = property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            _ = property.HasIndex(x => new { x.ApiResourceId, x.Key }).IsUnique();
        });

        _ = modelBuilder.Entity<ApiScope>(scope =>
        {
            _ = scope.ToTable(storeOptions.ApiScope).HasKey(x => x.Id);

            _ = scope.Property(x => x.Name).HasMaxLength(200).IsRequired();
            _ = scope.Property(x => x.DisplayName).HasMaxLength(200);
            _ = scope.Property(x => x.Description).HasMaxLength(1000);

            _ = scope.HasIndex(x => x.Name).IsUnique();

            _ = scope.HasMany(x => x.UserClaims).WithOne(x => x.Scope).HasForeignKey(x => x.ScopeId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });
        _ = modelBuilder.Entity<ApiScopeClaim>(scopeClaim =>
        {
            _ = scopeClaim.ToTable(storeOptions.ApiScopeClaim).HasKey(x => x.Id);

            _ = scopeClaim.Property(x => x.Type).HasMaxLength(200).IsRequired();

            _ = scopeClaim.HasIndex(x => new { x.ScopeId, x.Type }).IsUnique();
        });
        _ = modelBuilder.Entity<ApiScopeProperty>(property =>
        {
            _ = property.ToTable(storeOptions.ApiScopeProperty).HasKey(x => x.Id);
            _ = property.Property(x => x.Key).HasMaxLength(250).IsRequired();
            _ = property.Property(x => x.Value).HasMaxLength(2000).IsRequired();

            _ = property.HasIndex(x => new { x.ScopeId, x.Key }).IsUnique();
        });
    }

    /// <summary>
    /// Configures the identity providers.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="storeOptions">The store options.</param>
    public static void ConfigureIdentityProviderContext(this ModelBuilder modelBuilder, ConfigurationStoreOptions storeOptions)
    {
        if (!string.IsNullOrWhiteSpace(storeOptions.DefaultSchema))
        {
            _ = modelBuilder.HasDefaultSchema(storeOptions.DefaultSchema);
        }

        _ = modelBuilder.Entity<IdentityProvider>(entity =>
        {
            _ = entity.ToTable(storeOptions.IdentityProvider).HasKey(x => x.Id);

            _ = entity.Property(x => x.Scheme).HasMaxLength(200).IsRequired();
            _ = entity.Property(x => x.Type).HasMaxLength(20).IsRequired();
            _ = entity.Property(x => x.DisplayName).HasMaxLength(200);

            _ = entity.HasIndex(x => x.Scheme).IsUnique();
        });
    }
}
