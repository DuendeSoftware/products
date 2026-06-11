using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServerTemplate.Migrations.ConfigurationDb;

/// <inheritdoc />
public partial class Configuration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "ApiResources",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                AllowedAccessTokenSigningAlgorithms = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                ShowInDiscoveryDocument = table.Column<bool>(type: "INTEGER", nullable: false),
                RequireResourceIndicator = table.Column<bool>(type: "INTEGER", nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastAccessed = table.Column<DateTime>(type: "TEXT", nullable: true),
                NonEditable = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiResources", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "ApiScopes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Required = table.Column<bool>(type: "INTEGER", nullable: false),
                Emphasize = table.Column<bool>(type: "INTEGER", nullable: false),
                ShowInDiscoveryDocument = table.Column<bool>(type: "INTEGER", nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastAccessed = table.Column<DateTime>(type: "TEXT", nullable: true),
                NonEditable = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiScopes", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "Clients",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                ClientId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ProtocolType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                RequireClientSecret = table.Column<bool>(type: "INTEGER", nullable: false),
                ClientName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                ClientUri = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                LogoUri = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                RequireConsent = table.Column<bool>(type: "INTEGER", nullable: false),
                AllowRememberConsent = table.Column<bool>(type: "INTEGER", nullable: false),
                AlwaysIncludeUserClaimsInIdToken = table.Column<bool>(type: "INTEGER", nullable: false),
                RequirePkce = table.Column<bool>(type: "INTEGER", nullable: false),
                AllowPlainTextPkce = table.Column<bool>(type: "INTEGER", nullable: false),
                RequireRequestObject = table.Column<bool>(type: "INTEGER", nullable: false),
                AllowAccessTokensViaBrowser = table.Column<bool>(type: "INTEGER", nullable: false),
                RequireDPoP = table.Column<bool>(type: "INTEGER", nullable: false),
                DPoPValidationMode = table.Column<int>(type: "INTEGER", nullable: false),
                DPoPClockSkew = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                FrontChannelLogoutUri = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                FrontChannelLogoutSessionRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                BackChannelLogoutUri = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                BackChannelLogoutSessionRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                AllowOfflineAccess = table.Column<bool>(type: "INTEGER", nullable: false),
                IdentityTokenLifetime = table.Column<int>(type: "INTEGER", nullable: false),
                AllowedIdentityTokenSigningAlgorithms = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                AccessTokenLifetime = table.Column<int>(type: "INTEGER", nullable: false),
                AuthorizationCodeLifetime = table.Column<int>(type: "INTEGER", nullable: false),
                ConsentLifetime = table.Column<int>(type: "INTEGER", nullable: true),
                AbsoluteRefreshTokenLifetime = table.Column<int>(type: "INTEGER", nullable: false),
                SlidingRefreshTokenLifetime = table.Column<int>(type: "INTEGER", nullable: false),
                RefreshTokenUsage = table.Column<int>(type: "INTEGER", nullable: false),
                UpdateAccessTokenClaimsOnRefresh = table.Column<bool>(type: "INTEGER", nullable: false),
                RefreshTokenExpiration = table.Column<int>(type: "INTEGER", nullable: false),
                AccessTokenType = table.Column<int>(type: "INTEGER", nullable: false),
                EnableLocalLogin = table.Column<bool>(type: "INTEGER", nullable: false),
                IncludeJwtId = table.Column<bool>(type: "INTEGER", nullable: false),
                AlwaysSendClientClaims = table.Column<bool>(type: "INTEGER", nullable: false),
                ClientClaimsPrefix = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                PairWiseSubjectSalt = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                InitiateLoginUri = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                UserSsoLifetime = table.Column<int>(type: "INTEGER", nullable: true),
                UserCodeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                DeviceCodeLifetime = table.Column<int>(type: "INTEGER", nullable: false),
                CibaLifetime = table.Column<int>(type: "INTEGER", nullable: true),
                PollingInterval = table.Column<int>(type: "INTEGER", nullable: true),
                CoordinateLifetimeWithUserSession = table.Column<bool>(type: "INTEGER", nullable: true),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastAccessed = table.Column<DateTime>(type: "TEXT", nullable: true),
                NonEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                PushedAuthorizationLifetime = table.Column<int>(type: "INTEGER", nullable: true),
                RequirePushedAuthorization = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Clients", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "IdentityProviders",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Scheme = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Properties = table.Column<string>(type: "TEXT", nullable: true),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastAccessed = table.Column<DateTime>(type: "TEXT", nullable: true),
                NonEditable = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_IdentityProviders", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "IdentityResources",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Required = table.Column<bool>(type: "INTEGER", nullable: false),
                Emphasize = table.Column<bool>(type: "INTEGER", nullable: false),
                ShowInDiscoveryDocument = table.Column<bool>(type: "INTEGER", nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTime>(type: "TEXT", nullable: true),
                NonEditable = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_IdentityResources", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlServiceProviders",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                EntityId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                ClockSkewSeconds = table.Column<double>(type: "REAL", nullable: true),
                RequestMaxAgeSeconds = table.Column<double>(type: "REAL", nullable: true),
                AssertionLifetimeSeconds = table.Column<double>(type: "REAL", nullable: true),
                RequireSignedAuthnRequests = table.Column<bool>(type: "INTEGER", nullable: true),
                RequireSignedLogoutResponses = table.Column<bool>(type: "INTEGER", nullable: true),
                AllowIdpInitiated = table.Column<bool>(type: "INTEGER", nullable: false),
                DefaultNameIdFormat = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                EmailNameIdClaimType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                SigningBehavior = table.Column<int>(type: "INTEGER", nullable: true),
                AllowedSignatureAlgorithms = table.Column<string>(type: "TEXT", nullable: true),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastAccessed = table.Column<DateTime>(type: "TEXT", nullable: true),
                NonEditable = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlServiceProviders", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "ApiResourceClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApiResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiResourceClaims", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ApiResourceClaims_ApiResources_ApiResourceId",
                    column: x => x.ApiResourceId,
                    principalTable: "ApiResources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ApiResourceProperties",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApiResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                Key = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiResourceProperties", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ApiResourceProperties_ApiResources_ApiResourceId",
                    column: x => x.ApiResourceId,
                    principalTable: "ApiResources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ApiResourceScopes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Scope = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ApiResourceId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiResourceScopes", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ApiResourceScopes_ApiResources_ApiResourceId",
                    column: x => x.ApiResourceId,
                    principalTable: "ApiResources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ApiResourceSecrets",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApiResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                Expiration = table.Column<DateTime>(type: "TEXT", nullable: true),
                Type = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiResourceSecrets", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ApiResourceSecrets_ApiResources_ApiResourceId",
                    column: x => x.ApiResourceId,
                    principalTable: "ApiResources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ApiScopeClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ScopeId = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiScopeClaims", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ApiScopeClaims_ApiScopes_ScopeId",
                    column: x => x.ScopeId,
                    principalTable: "ApiScopes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ApiScopeProperties",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ScopeId = table.Column<int>(type: "INTEGER", nullable: false),
                Key = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ApiScopeProperties", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ApiScopeProperties_ApiScopes_ScopeId",
                    column: x => x.ScopeId,
                    principalTable: "ApiScopes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Type = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientClaims", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientClaims_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientCorsOrigins",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Origin = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientCorsOrigins", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientCorsOrigins_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientGrantTypes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GrantType = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientGrantTypes", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientGrantTypes_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientIdPRestrictions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientIdPRestrictions", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientIdPRestrictions_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientPostLogoutRedirectUris",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PostLogoutRedirectUri = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientPostLogoutRedirectUris", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientPostLogoutRedirectUris_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientProperties",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                Key = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientProperties", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientProperties_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientRedirectUris",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                RedirectUri = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientRedirectUris", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientRedirectUris_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientScopes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Scope = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientScopes", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientScopes_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "ClientSecrets",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                Expiration = table.Column<DateTime>(type: "TEXT", nullable: true),
                Type = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ClientSecrets", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_ClientSecrets_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "IdentityResourceClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                IdentityResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_IdentityResourceClaims", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_IdentityResourceClaims_IdentityResources_IdentityResourceId",
                    column: x => x.IdentityResourceId,
                    principalTable: "IdentityResources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "IdentityResourceProperties",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                IdentityResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                Key = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_IdentityResourceProperties", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_IdentityResourceProperties_IdentityResources_IdentityResourceId",
                    column: x => x.IdentityResourceId,
                    principalTable: "IdentityResources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlAllowedScopes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Scope = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SamlServiceProviderId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlAllowedScopes", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlAllowedScopes_SamlServiceProviders_SamlServiceProviderId",
                    column: x => x.SamlServiceProviderId,
                    principalTable: "SamlServiceProviders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlAssertionConsumerServices",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Location = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                Binding = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Index = table.Column<int>(type: "INTEGER", nullable: false),
                IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                SamlServiceProviderId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlAssertionConsumerServices", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlAssertionConsumerServices_SamlServiceProviders_SamlServiceProviderId",
                    column: x => x.SamlServiceProviderId,
                    principalTable: "SamlServiceProviders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlAuthnContextMappings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                OidcValue = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                SamlAuthnContextClassRef = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                SamlServiceProviderId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlAuthnContextMappings", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlAuthnContextMappings_SamlServiceProviders_SamlServiceProviderId",
                    column: x => x.SamlServiceProviderId,
                    principalTable: "SamlServiceProviders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlCertificates",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Data = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                Use = table.Column<int>(type: "INTEGER", nullable: false),
                SamlServiceProviderId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlCertificates", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlCertificates_SamlServiceProviders_SamlServiceProviderId",
                    column: x => x.SamlServiceProviderId,
                    principalTable: "SamlServiceProviders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlClaimMappings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ClaimType = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                SamlAttributeName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                SamlServiceProviderId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlClaimMappings", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlClaimMappings_SamlServiceProviders_SamlServiceProviderId",
                    column: x => x.SamlServiceProviderId,
                    principalTable: "SamlServiceProviders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlRequestedClaimTypes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ClaimType = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                SamlServiceProviderId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlRequestedClaimTypes", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlRequestedClaimTypes_SamlServiceProviders_SamlServiceProviderId",
                    column: x => x.SamlServiceProviderId,
                    principalTable: "SamlServiceProviders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlSingleLogoutServices",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Location = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                Binding = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SamlServiceProviderId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlSingleLogoutServices", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlSingleLogoutServices_SamlServiceProviders_SamlServiceProviderId",
                    column: x => x.SamlServiceProviderId,
                    principalTable: "SamlServiceProviders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiResourceClaims_ApiResourceId_Type",
            table: "ApiResourceClaims",
            columns: new[] { "ApiResourceId", "Type" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiResourceProperties_ApiResourceId_Key",
            table: "ApiResourceProperties",
            columns: new[] { "ApiResourceId", "Key" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiResources_Name",
            table: "ApiResources",
            column: "Name",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiResourceScopes_ApiResourceId_Scope",
            table: "ApiResourceScopes",
            columns: new[] { "ApiResourceId", "Scope" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiResourceSecrets_ApiResourceId",
            table: "ApiResourceSecrets",
            column: "ApiResourceId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiScopeClaims_ScopeId_Type",
            table: "ApiScopeClaims",
            columns: new[] { "ScopeId", "Type" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiScopeProperties_ScopeId_Key",
            table: "ApiScopeProperties",
            columns: new[] { "ScopeId", "Key" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ApiScopes_Name",
            table: "ApiScopes",
            column: "Name",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientClaims_ClientId_Type_Value",
            table: "ClientClaims",
            columns: new[] { "ClientId", "Type", "Value" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientCorsOrigins_ClientId_Origin",
            table: "ClientCorsOrigins",
            columns: new[] { "ClientId", "Origin" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientGrantTypes_ClientId_GrantType",
            table: "ClientGrantTypes",
            columns: new[] { "ClientId", "GrantType" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientIdPRestrictions_ClientId_Provider",
            table: "ClientIdPRestrictions",
            columns: new[] { "ClientId", "Provider" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientPostLogoutRedirectUris_ClientId_PostLogoutRedirectUri",
            table: "ClientPostLogoutRedirectUris",
            columns: new[] { "ClientId", "PostLogoutRedirectUri" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientProperties_ClientId_Key",
            table: "ClientProperties",
            columns: new[] { "ClientId", "Key" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientRedirectUris_ClientId_RedirectUri",
            table: "ClientRedirectUris",
            columns: new[] { "ClientId", "RedirectUri" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_Clients_ClientId",
            table: "Clients",
            column: "ClientId",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientScopes_ClientId_Scope",
            table: "ClientScopes",
            columns: new[] { "ClientId", "Scope" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ClientSecrets_ClientId",
            table: "ClientSecrets",
            column: "ClientId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_IdentityProviders_Scheme",
            table: "IdentityProviders",
            column: "Scheme",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_IdentityResourceClaims_IdentityResourceId_Type",
            table: "IdentityResourceClaims",
            columns: new[] { "IdentityResourceId", "Type" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_IdentityResourceProperties_IdentityResourceId_Key",
            table: "IdentityResourceProperties",
            columns: new[] { "IdentityResourceId", "Key" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_IdentityResources_Name",
            table: "IdentityResources",
            column: "Name",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlAllowedScopes_SamlServiceProviderId_Scope",
            table: "SamlAllowedScopes",
            columns: new[] { "SamlServiceProviderId", "Scope" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlAssertionConsumerServices_SamlServiceProviderId_Location",
            table: "SamlAssertionConsumerServices",
            columns: new[] { "SamlServiceProviderId", "Location" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlAuthnContextMappings_SamlServiceProviderId_OidcValue",
            table: "SamlAuthnContextMappings",
            columns: new[] { "SamlServiceProviderId", "OidcValue" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlCertificates_SamlServiceProviderId",
            table: "SamlCertificates",
            column: "SamlServiceProviderId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlClaimMappings_SamlServiceProviderId_ClaimType",
            table: "SamlClaimMappings",
            columns: new[] { "SamlServiceProviderId", "ClaimType" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlRequestedClaimTypes_SamlServiceProviderId_ClaimType",
            table: "SamlRequestedClaimTypes",
            columns: new[] { "SamlServiceProviderId", "ClaimType" },
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlServiceProviders_EntityId",
            table: "SamlServiceProviders",
            column: "EntityId",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlSingleLogoutServices_SamlServiceProviderId_Binding",
            table: "SamlSingleLogoutServices",
            columns: new[] { "SamlServiceProviderId", "Binding" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(
            name: "ApiResourceClaims");

        _ = migrationBuilder.DropTable(
            name: "ApiResourceProperties");

        _ = migrationBuilder.DropTable(
            name: "ApiResourceScopes");

        _ = migrationBuilder.DropTable(
            name: "ApiResourceSecrets");

        _ = migrationBuilder.DropTable(
            name: "ApiScopeClaims");

        _ = migrationBuilder.DropTable(
            name: "ApiScopeProperties");

        _ = migrationBuilder.DropTable(
            name: "ClientClaims");

        _ = migrationBuilder.DropTable(
            name: "ClientCorsOrigins");

        _ = migrationBuilder.DropTable(
            name: "ClientGrantTypes");

        _ = migrationBuilder.DropTable(
            name: "ClientIdPRestrictions");

        _ = migrationBuilder.DropTable(
            name: "ClientPostLogoutRedirectUris");

        _ = migrationBuilder.DropTable(
            name: "ClientProperties");

        _ = migrationBuilder.DropTable(
            name: "ClientRedirectUris");

        _ = migrationBuilder.DropTable(
            name: "ClientScopes");

        _ = migrationBuilder.DropTable(
            name: "ClientSecrets");

        _ = migrationBuilder.DropTable(
            name: "IdentityProviders");

        _ = migrationBuilder.DropTable(
            name: "IdentityResourceClaims");

        _ = migrationBuilder.DropTable(
            name: "IdentityResourceProperties");

        _ = migrationBuilder.DropTable(
            name: "SamlAllowedScopes");

        _ = migrationBuilder.DropTable(
            name: "SamlAssertionConsumerServices");

        _ = migrationBuilder.DropTable(
            name: "SamlAuthnContextMappings");

        _ = migrationBuilder.DropTable(
            name: "SamlCertificates");

        _ = migrationBuilder.DropTable(
            name: "SamlClaimMappings");

        _ = migrationBuilder.DropTable(
            name: "SamlRequestedClaimTypes");

        _ = migrationBuilder.DropTable(
            name: "SamlSingleLogoutServices");

        _ = migrationBuilder.DropTable(
            name: "ApiResources");

        _ = migrationBuilder.DropTable(
            name: "ApiScopes");

        _ = migrationBuilder.DropTable(
            name: "Clients");

        _ = migrationBuilder.DropTable(
            name: "IdentityResources");

        _ = migrationBuilder.DropTable(
            name: "SamlServiceProviders");
    }
}
