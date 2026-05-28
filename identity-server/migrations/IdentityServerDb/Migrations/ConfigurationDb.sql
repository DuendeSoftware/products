IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ApiResources] (
    [Id] int NOT NULL IDENTITY,
    [Enabled] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [DisplayName] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [AllowedAccessTokenSigningAlgorithms] nvarchar(100) NULL,
    [ShowInDiscoveryDocument] bit NOT NULL,
    [RequireResourceIndicator] bit NOT NULL,
    [Created] datetime2 NOT NULL,
    [Updated] datetime2 NULL,
    [LastAccessed] datetime2 NULL,
    [NonEditable] bit NOT NULL,
    CONSTRAINT [PK_ApiResources] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ApiScopes] (
    [Id] int NOT NULL IDENTITY,
    [Enabled] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [DisplayName] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [Required] bit NOT NULL,
    [Emphasize] bit NOT NULL,
    [ShowInDiscoveryDocument] bit NOT NULL,
    [Created] datetime2 NOT NULL,
    [Updated] datetime2 NULL,
    [LastAccessed] datetime2 NULL,
    [NonEditable] bit NOT NULL,
    CONSTRAINT [PK_ApiScopes] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Clients] (
    [Id] int NOT NULL IDENTITY,
    [Enabled] bit NOT NULL,
    [ClientId] nvarchar(200) NOT NULL,
    [ProtocolType] nvarchar(200) NOT NULL,
    [RequireClientSecret] bit NOT NULL,
    [ClientName] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [ClientUri] nvarchar(2000) NULL,
    [LogoUri] nvarchar(2000) NULL,
    [RequireConsent] bit NOT NULL,
    [AllowRememberConsent] bit NOT NULL,
    [AlwaysIncludeUserClaimsInIdToken] bit NOT NULL,
    [RequirePkce] bit NOT NULL,
    [AllowPlainTextPkce] bit NOT NULL,
    [RequireRequestObject] bit NOT NULL,
    [AllowAccessTokensViaBrowser] bit NOT NULL,
    [RequireDPoP] bit NOT NULL,
    [DPoPValidationMode] int NOT NULL,
    [DPoPClockSkew] time NOT NULL,
    [FrontChannelLogoutUri] nvarchar(2000) NULL,
    [FrontChannelLogoutSessionRequired] bit NOT NULL,
    [BackChannelLogoutUri] nvarchar(2000) NULL,
    [BackChannelLogoutSessionRequired] bit NOT NULL,
    [AllowOfflineAccess] bit NOT NULL,
    [IdentityTokenLifetime] int NOT NULL,
    [AllowedIdentityTokenSigningAlgorithms] nvarchar(100) NULL,
    [AccessTokenLifetime] int NOT NULL,
    [AuthorizationCodeLifetime] int NOT NULL,
    [ConsentLifetime] int NULL,
    [AbsoluteRefreshTokenLifetime] int NOT NULL,
    [SlidingRefreshTokenLifetime] int NOT NULL,
    [RefreshTokenUsage] int NOT NULL,
    [UpdateAccessTokenClaimsOnRefresh] bit NOT NULL,
    [RefreshTokenExpiration] int NOT NULL,
    [AccessTokenType] int NOT NULL,
    [EnableLocalLogin] bit NOT NULL,
    [IncludeJwtId] bit NOT NULL,
    [AlwaysSendClientClaims] bit NOT NULL,
    [ClientClaimsPrefix] nvarchar(200) NULL,
    [PairWiseSubjectSalt] nvarchar(200) NULL,
    [InitiateLoginUri] nvarchar(2000) NULL,
    [UserSsoLifetime] int NULL,
    [UserCodeType] nvarchar(100) NULL,
    [DeviceCodeLifetime] int NOT NULL,
    [CibaLifetime] int NULL,
    [PollingInterval] int NULL,
    [CoordinateLifetimeWithUserSession] bit NULL,
    [Created] datetime2 NOT NULL,
    [Updated] datetime2 NULL,
    [LastAccessed] datetime2 NULL,
    [NonEditable] bit NOT NULL,
    [PushedAuthorizationLifetime] int NULL,
    [RequirePushedAuthorization] bit NOT NULL,
    CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [IdentityProviders] (
    [Id] int NOT NULL IDENTITY,
    [Scheme] nvarchar(200) NOT NULL,
    [DisplayName] nvarchar(200) NULL,
    [Enabled] bit NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [Properties] nvarchar(max) NULL,
    [Created] datetime2 NOT NULL,
    [Updated] datetime2 NULL,
    [LastAccessed] datetime2 NULL,
    [NonEditable] bit NOT NULL,
    CONSTRAINT [PK_IdentityProviders] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [IdentityResources] (
    [Id] int NOT NULL IDENTITY,
    [Enabled] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [DisplayName] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [Required] bit NOT NULL,
    [Emphasize] bit NOT NULL,
    [ShowInDiscoveryDocument] bit NOT NULL,
    [Created] datetime2 NOT NULL,
    [Updated] datetime2 NULL,
    [NonEditable] bit NOT NULL,
    CONSTRAINT [PK_IdentityResources] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ApiResourceClaims] (
    [Id] int NOT NULL IDENTITY,
    [ApiResourceId] int NOT NULL,
    [Type] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_ApiResourceClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiResourceClaims_ApiResources_ApiResourceId] FOREIGN KEY ([ApiResourceId]) REFERENCES [ApiResources] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ApiResourceProperties] (
    [Id] int NOT NULL IDENTITY,
    [ApiResourceId] int NOT NULL,
    [Key] nvarchar(250) NOT NULL,
    [Value] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_ApiResourceProperties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiResourceProperties_ApiResources_ApiResourceId] FOREIGN KEY ([ApiResourceId]) REFERENCES [ApiResources] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ApiResourceScopes] (
    [Id] int NOT NULL IDENTITY,
    [Scope] nvarchar(200) NOT NULL,
    [ApiResourceId] int NOT NULL,
    CONSTRAINT [PK_ApiResourceScopes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiResourceScopes_ApiResources_ApiResourceId] FOREIGN KEY ([ApiResourceId]) REFERENCES [ApiResources] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ApiResourceSecrets] (
    [Id] int NOT NULL IDENTITY,
    [ApiResourceId] int NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Value] nvarchar(4000) NOT NULL,
    [Expiration] datetime2 NULL,
    [Type] nvarchar(250) NOT NULL,
    [Created] datetime2 NOT NULL,
    CONSTRAINT [PK_ApiResourceSecrets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiResourceSecrets_ApiResources_ApiResourceId] FOREIGN KEY ([ApiResourceId]) REFERENCES [ApiResources] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ApiScopeClaims] (
    [Id] int NOT NULL IDENTITY,
    [ScopeId] int NOT NULL,
    [Type] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_ApiScopeClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiScopeClaims_ApiScopes_ScopeId] FOREIGN KEY ([ScopeId]) REFERENCES [ApiScopes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ApiScopeProperties] (
    [Id] int NOT NULL IDENTITY,
    [ScopeId] int NOT NULL,
    [Key] nvarchar(250) NOT NULL,
    [Value] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_ApiScopeProperties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiScopeProperties_ApiScopes_ScopeId] FOREIGN KEY ([ScopeId]) REFERENCES [ApiScopes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientClaims] (
    [Id] int NOT NULL IDENTITY,
    [Type] nvarchar(250) NOT NULL,
    [Value] nvarchar(250) NOT NULL,
    [ClientId] int NOT NULL,
    CONSTRAINT [PK_ClientClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientClaims_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientCorsOrigins] (
    [Id] int NOT NULL IDENTITY,
    [Origin] nvarchar(150) NOT NULL,
    [ClientId] int NOT NULL,
    CONSTRAINT [PK_ClientCorsOrigins] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientCorsOrigins_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientGrantTypes] (
    [Id] int NOT NULL IDENTITY,
    [GrantType] nvarchar(250) NOT NULL,
    [ClientId] int NOT NULL,
    CONSTRAINT [PK_ClientGrantTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientGrantTypes_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientIdPRestrictions] (
    [Id] int NOT NULL IDENTITY,
    [Provider] nvarchar(200) NOT NULL,
    [ClientId] int NOT NULL,
    CONSTRAINT [PK_ClientIdPRestrictions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientIdPRestrictions_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientPostLogoutRedirectUris] (
    [Id] int NOT NULL IDENTITY,
    [PostLogoutRedirectUri] nvarchar(400) NOT NULL,
    [ClientId] int NOT NULL,
    CONSTRAINT [PK_ClientPostLogoutRedirectUris] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientPostLogoutRedirectUris_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientProperties] (
    [Id] int NOT NULL IDENTITY,
    [ClientId] int NOT NULL,
    [Key] nvarchar(250) NOT NULL,
    [Value] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_ClientProperties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientProperties_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientRedirectUris] (
    [Id] int NOT NULL IDENTITY,
    [RedirectUri] nvarchar(400) NOT NULL,
    [ClientId] int NOT NULL,
    CONSTRAINT [PK_ClientRedirectUris] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientRedirectUris_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientScopes] (
    [Id] int NOT NULL IDENTITY,
    [Scope] nvarchar(200) NOT NULL,
    [ClientId] int NOT NULL,
    CONSTRAINT [PK_ClientScopes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientScopes_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ClientSecrets] (
    [Id] int NOT NULL IDENTITY,
    [ClientId] int NOT NULL,
    [Description] nvarchar(2000) NULL,
    [Value] nvarchar(4000) NOT NULL,
    [Expiration] datetime2 NULL,
    [Type] nvarchar(250) NOT NULL,
    [Created] datetime2 NOT NULL,
    CONSTRAINT [PK_ClientSecrets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientSecrets_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [IdentityResourceClaims] (
    [Id] int NOT NULL IDENTITY,
    [IdentityResourceId] int NOT NULL,
    [Type] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_IdentityResourceClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IdentityResourceClaims_IdentityResources_IdentityResourceId] FOREIGN KEY ([IdentityResourceId]) REFERENCES [IdentityResources] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [IdentityResourceProperties] (
    [Id] int NOT NULL IDENTITY,
    [IdentityResourceId] int NOT NULL,
    [Key] nvarchar(250) NOT NULL,
    [Value] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_IdentityResourceProperties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IdentityResourceProperties_IdentityResources_IdentityResourceId] FOREIGN KEY ([IdentityResourceId]) REFERENCES [IdentityResources] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_ApiResourceClaims_ApiResourceId_Type] ON [ApiResourceClaims] ([ApiResourceId], [Type]);
GO

CREATE UNIQUE INDEX [IX_ApiResourceProperties_ApiResourceId_Key] ON [ApiResourceProperties] ([ApiResourceId], [Key]);
GO

CREATE UNIQUE INDEX [IX_ApiResources_Name] ON [ApiResources] ([Name]);
GO

CREATE UNIQUE INDEX [IX_ApiResourceScopes_ApiResourceId_Scope] ON [ApiResourceScopes] ([ApiResourceId], [Scope]);
GO

CREATE INDEX [IX_ApiResourceSecrets_ApiResourceId] ON [ApiResourceSecrets] ([ApiResourceId]);
GO

CREATE UNIQUE INDEX [IX_ApiScopeClaims_ScopeId_Type] ON [ApiScopeClaims] ([ScopeId], [Type]);
GO

CREATE UNIQUE INDEX [IX_ApiScopeProperties_ScopeId_Key] ON [ApiScopeProperties] ([ScopeId], [Key]);
GO

CREATE UNIQUE INDEX [IX_ApiScopes_Name] ON [ApiScopes] ([Name]);
GO

CREATE UNIQUE INDEX [IX_ClientClaims_ClientId_Type_Value] ON [ClientClaims] ([ClientId], [Type], [Value]);
GO

CREATE UNIQUE INDEX [IX_ClientCorsOrigins_ClientId_Origin] ON [ClientCorsOrigins] ([ClientId], [Origin]);
GO

CREATE UNIQUE INDEX [IX_ClientGrantTypes_ClientId_GrantType] ON [ClientGrantTypes] ([ClientId], [GrantType]);
GO

CREATE UNIQUE INDEX [IX_ClientIdPRestrictions_ClientId_Provider] ON [ClientIdPRestrictions] ([ClientId], [Provider]);
GO

CREATE UNIQUE INDEX [IX_ClientPostLogoutRedirectUris_ClientId_PostLogoutRedirectUri] ON [ClientPostLogoutRedirectUris] ([ClientId], [PostLogoutRedirectUri]);
GO

CREATE UNIQUE INDEX [IX_ClientProperties_ClientId_Key] ON [ClientProperties] ([ClientId], [Key]);
GO

CREATE UNIQUE INDEX [IX_ClientRedirectUris_ClientId_RedirectUri] ON [ClientRedirectUris] ([ClientId], [RedirectUri]);
GO

CREATE UNIQUE INDEX [IX_Clients_ClientId] ON [Clients] ([ClientId]);
GO

CREATE UNIQUE INDEX [IX_ClientScopes_ClientId_Scope] ON [ClientScopes] ([ClientId], [Scope]);
GO

CREATE INDEX [IX_ClientSecrets_ClientId] ON [ClientSecrets] ([ClientId]);
GO

CREATE UNIQUE INDEX [IX_IdentityProviders_Scheme] ON [IdentityProviders] ([Scheme]);
GO

CREATE UNIQUE INDEX [IX_IdentityResourceClaims_IdentityResourceId_Type] ON [IdentityResourceClaims] ([IdentityResourceId], [Type]);
GO

CREATE UNIQUE INDEX [IX_IdentityResourceProperties_IdentityResourceId_Key] ON [IdentityResourceProperties] ([IdentityResourceId], [Key]);
GO

CREATE UNIQUE INDEX [IX_IdentityResources_Name] ON [IdentityResources] ([Name]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20240104192404_Configuration', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [SamlServiceProviders] (
    [Id] int NOT NULL IDENTITY,
    [EntityId] nvarchar(200) NOT NULL,
    [DisplayName] nvarchar(200) NULL,
    [Description] nvarchar(1000) NULL,
    [Enabled] bit NOT NULL,
    [ClockSkewSeconds] float NULL,
    [RequestMaxAgeSeconds] float NULL,
    [AssertionLifetimeSeconds] float NULL,
    [SingleLogoutServiceLocation] nvarchar(400) NULL,
    [SingleLogoutServiceBinding] int NULL,
    [RequireSignedAuthnRequests] bit NULL,
    [RequireSignedLogoutResponses] bit NULL,
    [AllowIdpInitiated] bit NOT NULL,
    [DefaultNameIdFormat] nvarchar(2000) NULL,
    [EmailNameIdClaimType] nvarchar(200) NULL,
    [SigningBehavior] int NULL,
    [AllowedSignatureAlgorithms] nvarchar(max) NULL,
    [Created] datetime2 NOT NULL,
    [Updated] datetime2 NULL,
    [LastAccessed] datetime2 NULL,
    [NonEditable] bit NOT NULL,
    CONSTRAINT [PK_SamlServiceProviders] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SamlAllowedScopes] (
    [Id] int NOT NULL IDENTITY,
    [Scope] nvarchar(200) NOT NULL,
    [SamlServiceProviderId] int NOT NULL,
    CONSTRAINT [PK_SamlAllowedScopes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SamlAllowedScopes_SamlServiceProviders_SamlServiceProviderId] FOREIGN KEY ([SamlServiceProviderId]) REFERENCES [SamlServiceProviders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SamlAssertionConsumerServices] (
    [Id] int NOT NULL IDENTITY,
    [Location] nvarchar(400) NOT NULL,
    [Binding] nvarchar(200) NOT NULL,
    [Index] int NOT NULL,
    [IsDefault] bit NOT NULL,
    [SamlServiceProviderId] int NOT NULL,
    CONSTRAINT [PK_SamlAssertionConsumerServices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SamlAssertionConsumerServices_SamlServiceProviders_SamlServiceProviderId] FOREIGN KEY ([SamlServiceProviderId]) REFERENCES [SamlServiceProviders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SamlAuthnContextMappings] (
    [Id] int NOT NULL IDENTITY,
    [OidcValue] nvarchar(250) NOT NULL,
    [SamlAuthnContextClassRef] nvarchar(500) NOT NULL,
    [SamlServiceProviderId] int NOT NULL,
    CONSTRAINT [PK_SamlAuthnContextMappings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SamlAuthnContextMappings_SamlServiceProviders_SamlServiceProviderId] FOREIGN KEY ([SamlServiceProviderId]) REFERENCES [SamlServiceProviders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SamlCertificates] (
    [Id] int NOT NULL IDENTITY,
    [Data] nvarchar(4000) NOT NULL,
    [Use] int NOT NULL,
    [SamlServiceProviderId] int NOT NULL,
    CONSTRAINT [PK_SamlCertificates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SamlCertificates_SamlServiceProviders_SamlServiceProviderId] FOREIGN KEY ([SamlServiceProviderId]) REFERENCES [SamlServiceProviders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SamlClaimMappings] (
    [Id] int NOT NULL IDENTITY,
    [ClaimType] nvarchar(250) NOT NULL,
    [SamlAttributeName] nvarchar(250) NOT NULL,
    [SamlServiceProviderId] int NOT NULL,
    CONSTRAINT [PK_SamlClaimMappings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SamlClaimMappings_SamlServiceProviders_SamlServiceProviderId] FOREIGN KEY ([SamlServiceProviderId]) REFERENCES [SamlServiceProviders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SamlRequestedClaimTypes] (
    [Id] int NOT NULL IDENTITY,
    [ClaimType] nvarchar(250) NOT NULL,
    [SamlServiceProviderId] int NOT NULL,
    CONSTRAINT [PK_SamlRequestedClaimTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SamlRequestedClaimTypes_SamlServiceProviders_SamlServiceProviderId] FOREIGN KEY ([SamlServiceProviderId]) REFERENCES [SamlServiceProviders] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_SamlAllowedScopes_SamlServiceProviderId_Scope] ON [SamlAllowedScopes] ([SamlServiceProviderId], [Scope]);
GO

CREATE UNIQUE INDEX [IX_SamlAssertionConsumerServices_SamlServiceProviderId_Location] ON [SamlAssertionConsumerServices] ([SamlServiceProviderId], [Location]);
GO

CREATE UNIQUE INDEX [IX_SamlAuthnContextMappings_SamlServiceProviderId_OidcValue] ON [SamlAuthnContextMappings] ([SamlServiceProviderId], [OidcValue]);
GO

CREATE INDEX [IX_SamlCertificates_SamlServiceProviderId] ON [SamlCertificates] ([SamlServiceProviderId]);
GO

CREATE UNIQUE INDEX [IX_SamlClaimMappings_SamlServiceProviderId_ClaimType] ON [SamlClaimMappings] ([SamlServiceProviderId], [ClaimType]);
GO

CREATE UNIQUE INDEX [IX_SamlRequestedClaimTypes_SamlServiceProviderId_ClaimType] ON [SamlRequestedClaimTypes] ([SamlServiceProviderId], [ClaimType]);
GO

CREATE UNIQUE INDEX [IX_SamlServiceProviders_EntityId] ON [SamlServiceProviders] ([EntityId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260508184433_AddSamlServiceProviders', N'10.0.4');
GO

COMMIT;
GO

