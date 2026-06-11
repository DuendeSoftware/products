CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "ApiResources" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiResources" PRIMARY KEY AUTOINCREMENT,
    "Enabled" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "DisplayName" TEXT NULL,
    "Description" TEXT NULL,
    "AllowedAccessTokenSigningAlgorithms" TEXT NULL,
    "ShowInDiscoveryDocument" INTEGER NOT NULL,
    "RequireResourceIndicator" INTEGER NOT NULL,
    "Created" TEXT NOT NULL,
    "Updated" TEXT NULL,
    "LastAccessed" TEXT NULL,
    "NonEditable" INTEGER NOT NULL
);

CREATE TABLE "ApiScopes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiScopes" PRIMARY KEY AUTOINCREMENT,
    "Enabled" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "DisplayName" TEXT NULL,
    "Description" TEXT NULL,
    "Required" INTEGER NOT NULL,
    "Emphasize" INTEGER NOT NULL,
    "ShowInDiscoveryDocument" INTEGER NOT NULL,
    "Created" TEXT NOT NULL,
    "Updated" TEXT NULL,
    "LastAccessed" TEXT NULL,
    "NonEditable" INTEGER NOT NULL
);

CREATE TABLE "Clients" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Clients" PRIMARY KEY AUTOINCREMENT,
    "Enabled" INTEGER NOT NULL,
    "ClientId" TEXT NOT NULL,
    "ProtocolType" TEXT NOT NULL,
    "RequireClientSecret" INTEGER NOT NULL,
    "ClientName" TEXT NULL,
    "Description" TEXT NULL,
    "ClientUri" TEXT NULL,
    "LogoUri" TEXT NULL,
    "RequireConsent" INTEGER NOT NULL,
    "AllowRememberConsent" INTEGER NOT NULL,
    "AlwaysIncludeUserClaimsInIdToken" INTEGER NOT NULL,
    "RequirePkce" INTEGER NOT NULL,
    "AllowPlainTextPkce" INTEGER NOT NULL,
    "RequireRequestObject" INTEGER NOT NULL,
    "AllowAccessTokensViaBrowser" INTEGER NOT NULL,
    "RequireDPoP" INTEGER NOT NULL,
    "DPoPValidationMode" INTEGER NOT NULL,
    "DPoPClockSkew" TEXT NOT NULL,
    "FrontChannelLogoutUri" TEXT NULL,
    "FrontChannelLogoutSessionRequired" INTEGER NOT NULL,
    "BackChannelLogoutUri" TEXT NULL,
    "BackChannelLogoutSessionRequired" INTEGER NOT NULL,
    "AllowOfflineAccess" INTEGER NOT NULL,
    "IdentityTokenLifetime" INTEGER NOT NULL,
    "AllowedIdentityTokenSigningAlgorithms" TEXT NULL,
    "AccessTokenLifetime" INTEGER NOT NULL,
    "AuthorizationCodeLifetime" INTEGER NOT NULL,
    "ConsentLifetime" INTEGER NULL,
    "AbsoluteRefreshTokenLifetime" INTEGER NOT NULL,
    "SlidingRefreshTokenLifetime" INTEGER NOT NULL,
    "RefreshTokenUsage" INTEGER NOT NULL,
    "UpdateAccessTokenClaimsOnRefresh" INTEGER NOT NULL,
    "RefreshTokenExpiration" INTEGER NOT NULL,
    "AccessTokenType" INTEGER NOT NULL,
    "EnableLocalLogin" INTEGER NOT NULL,
    "IncludeJwtId" INTEGER NOT NULL,
    "AlwaysSendClientClaims" INTEGER NOT NULL,
    "ClientClaimsPrefix" TEXT NULL,
    "PairWiseSubjectSalt" TEXT NULL,
    "InitiateLoginUri" TEXT NULL,
    "UserSsoLifetime" INTEGER NULL,
    "UserCodeType" TEXT NULL,
    "DeviceCodeLifetime" INTEGER NOT NULL,
    "CibaLifetime" INTEGER NULL,
    "PollingInterval" INTEGER NULL,
    "CoordinateLifetimeWithUserSession" INTEGER NULL,
    "Created" TEXT NOT NULL,
    "Updated" TEXT NULL,
    "LastAccessed" TEXT NULL,
    "NonEditable" INTEGER NOT NULL,
    "PushedAuthorizationLifetime" INTEGER NULL,
    "RequirePushedAuthorization" INTEGER NOT NULL
);

CREATE TABLE "IdentityProviders" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_IdentityProviders" PRIMARY KEY AUTOINCREMENT,
    "Scheme" TEXT NOT NULL,
    "DisplayName" TEXT NULL,
    "Enabled" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    "Properties" TEXT NULL,
    "Created" TEXT NOT NULL,
    "Updated" TEXT NULL,
    "LastAccessed" TEXT NULL,
    "NonEditable" INTEGER NOT NULL
);

CREATE TABLE "IdentityResources" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_IdentityResources" PRIMARY KEY AUTOINCREMENT,
    "Enabled" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "DisplayName" TEXT NULL,
    "Description" TEXT NULL,
    "Required" INTEGER NOT NULL,
    "Emphasize" INTEGER NOT NULL,
    "ShowInDiscoveryDocument" INTEGER NOT NULL,
    "Created" TEXT NOT NULL,
    "Updated" TEXT NULL,
    "NonEditable" INTEGER NOT NULL
);

CREATE TABLE "SamlServiceProviders" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlServiceProviders" PRIMARY KEY AUTOINCREMENT,
    "EntityId" TEXT NOT NULL,
    "DisplayName" TEXT NULL,
    "Description" TEXT NULL,
    "Enabled" INTEGER NOT NULL,
    "ClockSkewSeconds" REAL NULL,
    "RequestMaxAgeSeconds" REAL NULL,
    "AssertionLifetimeSeconds" REAL NULL,
    "RequireSignedAuthnRequests" INTEGER NULL,
    "RequireSignedLogoutResponses" INTEGER NULL,
    "AllowIdpInitiated" INTEGER NOT NULL,
    "DefaultNameIdFormat" TEXT NULL,
    "EmailNameIdClaimType" TEXT NULL,
    "SigningBehavior" INTEGER NULL,
    "AllowedSignatureAlgorithms" TEXT NULL,
    "Created" TEXT NOT NULL,
    "Updated" TEXT NULL,
    "LastAccessed" TEXT NULL,
    "NonEditable" INTEGER NOT NULL
);

CREATE TABLE "ApiResourceClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiResourceClaims" PRIMARY KEY AUTOINCREMENT,
    "ApiResourceId" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    CONSTRAINT "FK_ApiResourceClaims_ApiResources_ApiResourceId" FOREIGN KEY ("ApiResourceId") REFERENCES "ApiResources" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ApiResourceProperties" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiResourceProperties" PRIMARY KEY AUTOINCREMENT,
    "ApiResourceId" INTEGER NOT NULL,
    "Key" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    CONSTRAINT "FK_ApiResourceProperties_ApiResources_ApiResourceId" FOREIGN KEY ("ApiResourceId") REFERENCES "ApiResources" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ApiResourceScopes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiResourceScopes" PRIMARY KEY AUTOINCREMENT,
    "Scope" TEXT NOT NULL,
    "ApiResourceId" INTEGER NOT NULL,
    CONSTRAINT "FK_ApiResourceScopes_ApiResources_ApiResourceId" FOREIGN KEY ("ApiResourceId") REFERENCES "ApiResources" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ApiResourceSecrets" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiResourceSecrets" PRIMARY KEY AUTOINCREMENT,
    "ApiResourceId" INTEGER NOT NULL,
    "Description" TEXT NULL,
    "Value" TEXT NOT NULL,
    "Expiration" TEXT NULL,
    "Type" TEXT NOT NULL,
    "Created" TEXT NOT NULL,
    CONSTRAINT "FK_ApiResourceSecrets_ApiResources_ApiResourceId" FOREIGN KEY ("ApiResourceId") REFERENCES "ApiResources" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ApiScopeClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiScopeClaims" PRIMARY KEY AUTOINCREMENT,
    "ScopeId" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    CONSTRAINT "FK_ApiScopeClaims_ApiScopes_ScopeId" FOREIGN KEY ("ScopeId") REFERENCES "ApiScopes" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ApiScopeProperties" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApiScopeProperties" PRIMARY KEY AUTOINCREMENT,
    "ScopeId" INTEGER NOT NULL,
    "Key" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    CONSTRAINT "FK_ApiScopeProperties_ApiScopes_ScopeId" FOREIGN KEY ("ScopeId") REFERENCES "ApiScopes" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientClaims" PRIMARY KEY AUTOINCREMENT,
    "Type" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    "ClientId" INTEGER NOT NULL,
    CONSTRAINT "FK_ClientClaims_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientCorsOrigins" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientCorsOrigins" PRIMARY KEY AUTOINCREMENT,
    "Origin" TEXT NOT NULL,
    "ClientId" INTEGER NOT NULL,
    CONSTRAINT "FK_ClientCorsOrigins_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientGrantTypes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientGrantTypes" PRIMARY KEY AUTOINCREMENT,
    "GrantType" TEXT NOT NULL,
    "ClientId" INTEGER NOT NULL,
    CONSTRAINT "FK_ClientGrantTypes_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientIdPRestrictions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientIdPRestrictions" PRIMARY KEY AUTOINCREMENT,
    "Provider" TEXT NOT NULL,
    "ClientId" INTEGER NOT NULL,
    CONSTRAINT "FK_ClientIdPRestrictions_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientPostLogoutRedirectUris" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientPostLogoutRedirectUris" PRIMARY KEY AUTOINCREMENT,
    "PostLogoutRedirectUri" TEXT NOT NULL,
    "ClientId" INTEGER NOT NULL,
    CONSTRAINT "FK_ClientPostLogoutRedirectUris_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientProperties" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientProperties" PRIMARY KEY AUTOINCREMENT,
    "ClientId" INTEGER NOT NULL,
    "Key" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    CONSTRAINT "FK_ClientProperties_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientRedirectUris" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientRedirectUris" PRIMARY KEY AUTOINCREMENT,
    "RedirectUri" TEXT NOT NULL,
    "ClientId" INTEGER NOT NULL,
    CONSTRAINT "FK_ClientRedirectUris_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientScopes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientScopes" PRIMARY KEY AUTOINCREMENT,
    "Scope" TEXT NOT NULL,
    "ClientId" INTEGER NOT NULL,
    CONSTRAINT "FK_ClientScopes_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ClientSecrets" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientSecrets" PRIMARY KEY AUTOINCREMENT,
    "ClientId" INTEGER NOT NULL,
    "Description" TEXT NULL,
    "Value" TEXT NOT NULL,
    "Expiration" TEXT NULL,
    "Type" TEXT NOT NULL,
    "Created" TEXT NOT NULL,
    CONSTRAINT "FK_ClientSecrets_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
);

CREATE TABLE "IdentityResourceClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_IdentityResourceClaims" PRIMARY KEY AUTOINCREMENT,
    "IdentityResourceId" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    CONSTRAINT "FK_IdentityResourceClaims_IdentityResources_IdentityResourceId" FOREIGN KEY ("IdentityResourceId") REFERENCES "IdentityResources" ("Id") ON DELETE CASCADE
);

CREATE TABLE "IdentityResourceProperties" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_IdentityResourceProperties" PRIMARY KEY AUTOINCREMENT,
    "IdentityResourceId" INTEGER NOT NULL,
    "Key" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    CONSTRAINT "FK_IdentityResourceProperties_IdentityResources_IdentityResourceId" FOREIGN KEY ("IdentityResourceId") REFERENCES "IdentityResources" ("Id") ON DELETE CASCADE
);

CREATE TABLE "SamlAllowedScopes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlAllowedScopes" PRIMARY KEY AUTOINCREMENT,
    "Scope" TEXT NOT NULL,
    "SamlServiceProviderId" INTEGER NOT NULL,
    CONSTRAINT "FK_SamlAllowedScopes_SamlServiceProviders_SamlServiceProviderId" FOREIGN KEY ("SamlServiceProviderId") REFERENCES "SamlServiceProviders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "SamlAssertionConsumerServices" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlAssertionConsumerServices" PRIMARY KEY AUTOINCREMENT,
    "Location" TEXT NOT NULL,
    "Binding" TEXT NOT NULL,
    "Index" INTEGER NOT NULL,
    "IsDefault" INTEGER NOT NULL,
    "SamlServiceProviderId" INTEGER NOT NULL,
    CONSTRAINT "FK_SamlAssertionConsumerServices_SamlServiceProviders_SamlServiceProviderId" FOREIGN KEY ("SamlServiceProviderId") REFERENCES "SamlServiceProviders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "SamlAuthnContextMappings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlAuthnContextMappings" PRIMARY KEY AUTOINCREMENT,
    "OidcValue" TEXT NOT NULL,
    "SamlAuthnContextClassRef" TEXT NOT NULL,
    "SamlServiceProviderId" INTEGER NOT NULL,
    CONSTRAINT "FK_SamlAuthnContextMappings_SamlServiceProviders_SamlServiceProviderId" FOREIGN KEY ("SamlServiceProviderId") REFERENCES "SamlServiceProviders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "SamlCertificates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlCertificates" PRIMARY KEY AUTOINCREMENT,
    "Data" TEXT NOT NULL,
    "Use" INTEGER NOT NULL,
    "SamlServiceProviderId" INTEGER NOT NULL,
    CONSTRAINT "FK_SamlCertificates_SamlServiceProviders_SamlServiceProviderId" FOREIGN KEY ("SamlServiceProviderId") REFERENCES "SamlServiceProviders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "SamlClaimMappings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlClaimMappings" PRIMARY KEY AUTOINCREMENT,
    "ClaimType" TEXT NOT NULL,
    "SamlAttributeName" TEXT NOT NULL,
    "SamlServiceProviderId" INTEGER NOT NULL,
    CONSTRAINT "FK_SamlClaimMappings_SamlServiceProviders_SamlServiceProviderId" FOREIGN KEY ("SamlServiceProviderId") REFERENCES "SamlServiceProviders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "SamlRequestedClaimTypes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlRequestedClaimTypes" PRIMARY KEY AUTOINCREMENT,
    "ClaimType" TEXT NOT NULL,
    "SamlServiceProviderId" INTEGER NOT NULL,
    CONSTRAINT "FK_SamlRequestedClaimTypes_SamlServiceProviders_SamlServiceProviderId" FOREIGN KEY ("SamlServiceProviderId") REFERENCES "SamlServiceProviders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "SamlSingleLogoutServices" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SamlSingleLogoutServices" PRIMARY KEY AUTOINCREMENT,
    "Location" TEXT NOT NULL,
    "Binding" TEXT NOT NULL,
    "SamlServiceProviderId" INTEGER NOT NULL,
    CONSTRAINT "FK_SamlSingleLogoutServices_SamlServiceProviders_SamlServiceProviderId" FOREIGN KEY ("SamlServiceProviderId") REFERENCES "SamlServiceProviders" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_ApiResourceClaims_ApiResourceId_Type" ON "ApiResourceClaims" ("ApiResourceId", "Type");

CREATE UNIQUE INDEX "IX_ApiResourceProperties_ApiResourceId_Key" ON "ApiResourceProperties" ("ApiResourceId", "Key");

CREATE UNIQUE INDEX "IX_ApiResources_Name" ON "ApiResources" ("Name");

CREATE UNIQUE INDEX "IX_ApiResourceScopes_ApiResourceId_Scope" ON "ApiResourceScopes" ("ApiResourceId", "Scope");

CREATE INDEX "IX_ApiResourceSecrets_ApiResourceId" ON "ApiResourceSecrets" ("ApiResourceId");

CREATE UNIQUE INDEX "IX_ApiScopeClaims_ScopeId_Type" ON "ApiScopeClaims" ("ScopeId", "Type");

CREATE UNIQUE INDEX "IX_ApiScopeProperties_ScopeId_Key" ON "ApiScopeProperties" ("ScopeId", "Key");

CREATE UNIQUE INDEX "IX_ApiScopes_Name" ON "ApiScopes" ("Name");

CREATE UNIQUE INDEX "IX_ClientClaims_ClientId_Type_Value" ON "ClientClaims" ("ClientId", "Type", "Value");

CREATE UNIQUE INDEX "IX_ClientCorsOrigins_ClientId_Origin" ON "ClientCorsOrigins" ("ClientId", "Origin");

CREATE UNIQUE INDEX "IX_ClientGrantTypes_ClientId_GrantType" ON "ClientGrantTypes" ("ClientId", "GrantType");

CREATE UNIQUE INDEX "IX_ClientIdPRestrictions_ClientId_Provider" ON "ClientIdPRestrictions" ("ClientId", "Provider");

CREATE UNIQUE INDEX "IX_ClientPostLogoutRedirectUris_ClientId_PostLogoutRedirectUri" ON "ClientPostLogoutRedirectUris" ("ClientId", "PostLogoutRedirectUri");

CREATE UNIQUE INDEX "IX_ClientProperties_ClientId_Key" ON "ClientProperties" ("ClientId", "Key");

CREATE UNIQUE INDEX "IX_ClientRedirectUris_ClientId_RedirectUri" ON "ClientRedirectUris" ("ClientId", "RedirectUri");

CREATE UNIQUE INDEX "IX_Clients_ClientId" ON "Clients" ("ClientId");

CREATE UNIQUE INDEX "IX_ClientScopes_ClientId_Scope" ON "ClientScopes" ("ClientId", "Scope");

CREATE INDEX "IX_ClientSecrets_ClientId" ON "ClientSecrets" ("ClientId");

CREATE UNIQUE INDEX "IX_IdentityProviders_Scheme" ON "IdentityProviders" ("Scheme");

CREATE UNIQUE INDEX "IX_IdentityResourceClaims_IdentityResourceId_Type" ON "IdentityResourceClaims" ("IdentityResourceId", "Type");

CREATE UNIQUE INDEX "IX_IdentityResourceProperties_IdentityResourceId_Key" ON "IdentityResourceProperties" ("IdentityResourceId", "Key");

CREATE UNIQUE INDEX "IX_IdentityResources_Name" ON "IdentityResources" ("Name");

CREATE UNIQUE INDEX "IX_SamlAllowedScopes_SamlServiceProviderId_Scope" ON "SamlAllowedScopes" ("SamlServiceProviderId", "Scope");

CREATE UNIQUE INDEX "IX_SamlAssertionConsumerServices_SamlServiceProviderId_Location" ON "SamlAssertionConsumerServices" ("SamlServiceProviderId", "Location");

CREATE UNIQUE INDEX "IX_SamlAuthnContextMappings_SamlServiceProviderId_OidcValue" ON "SamlAuthnContextMappings" ("SamlServiceProviderId", "OidcValue");

CREATE INDEX "IX_SamlCertificates_SamlServiceProviderId" ON "SamlCertificates" ("SamlServiceProviderId");

CREATE UNIQUE INDEX "IX_SamlClaimMappings_SamlServiceProviderId_ClaimType" ON "SamlClaimMappings" ("SamlServiceProviderId", "ClaimType");

CREATE UNIQUE INDEX "IX_SamlRequestedClaimTypes_SamlServiceProviderId_ClaimType" ON "SamlRequestedClaimTypes" ("SamlServiceProviderId", "ClaimType");

CREATE UNIQUE INDEX "IX_SamlServiceProviders_EntityId" ON "SamlServiceProviders" ("EntityId");

CREATE UNIQUE INDEX "IX_SamlSingleLogoutServices_SamlServiceProviderId_Binding" ON "SamlSingleLogoutServices" ("SamlServiceProviderId", "Binding");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260603060554_Configuration', '10.0.7');

COMMIT;

