using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServerTemplate.Migrations.PersistedGrantDb;

/// <inheritdoc />
public partial class Grants : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "DeviceCodes",
            columns: table => new
            {
                UserCode = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                DeviceCode = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SubjectId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                ClientId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                Expiration = table.Column<DateTime>(type: "TEXT", nullable: false),
                Data = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_DeviceCodes", x => x.UserCode);
            });

        _ = migrationBuilder.CreateTable(
            name: "Keys",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Version = table.Column<int>(type: "INTEGER", nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Use = table.Column<string>(type: "TEXT", nullable: true),
                Algorithm = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                IsX509Certificate = table.Column<bool>(type: "INTEGER", nullable: false),
                DataProtected = table.Column<bool>(type: "INTEGER", nullable: false),
                Data = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Keys", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "PersistedGrants",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                SubjectId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                ClientId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                Expiration = table.Column<DateTime>(type: "TEXT", nullable: true),
                ConsumedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                Data = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_PersistedGrants", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "PushedAuthorizationRequests",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ReferenceValueHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Parameters = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_PushedAuthorizationRequests", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlLogoutSessions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                LogoutId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SerializedSession = table.Column<string>(type: "TEXT", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Version = table.Column<uint>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlLogoutSessions", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlSigninStates",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                StateId = table.Column<Guid>(type: "TEXT", nullable: false),
                SerializedState = table.Column<string>(type: "TEXT", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ServiceProviderEntityId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlSigninStates", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "ServerSideSessions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Scheme = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SubjectId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                Renewed = table.Column<DateTime>(type: "TEXT", nullable: false),
                Expires = table.Column<DateTime>(type: "TEXT", nullable: true),
                Data = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ServerSideSessions", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "SamlLogoutSessionRequestIndices",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                RequestId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SamlLogoutSessionId = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_SamlLogoutSessionRequestIndices", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_SamlLogoutSessionRequestIndices_SamlLogoutSessions_SamlLogoutSessionId",
                    column: x => x.SamlLogoutSessionId,
                    principalTable: "SamlLogoutSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateIndex(
            name: "IX_DeviceCodes_DeviceCode",
            table: "DeviceCodes",
            column: "DeviceCode",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_DeviceCodes_Expiration",
            table: "DeviceCodes",
            column: "Expiration");

        _ = migrationBuilder.CreateIndex(
            name: "IX_Keys_Use",
            table: "Keys",
            column: "Use");

        _ = migrationBuilder.CreateIndex(
            name: "IX_PersistedGrants_ConsumedTime",
            table: "PersistedGrants",
            column: "ConsumedTime");

        _ = migrationBuilder.CreateIndex(
            name: "IX_PersistedGrants_Expiration",
            table: "PersistedGrants",
            column: "Expiration");

        _ = migrationBuilder.CreateIndex(
            name: "IX_PersistedGrants_Key",
            table: "PersistedGrants",
            column: "Key",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_PersistedGrants_SubjectId_ClientId_Type",
            table: "PersistedGrants",
            columns: new[] { "SubjectId", "ClientId", "Type" });

        _ = migrationBuilder.CreateIndex(
            name: "IX_PersistedGrants_SubjectId_SessionId_Type",
            table: "PersistedGrants",
            columns: new[] { "SubjectId", "SessionId", "Type" });

        _ = migrationBuilder.CreateIndex(
            name: "IX_PushedAuthorizationRequests_ExpiresAtUtc",
            table: "PushedAuthorizationRequests",
            column: "ExpiresAtUtc");

        _ = migrationBuilder.CreateIndex(
            name: "IX_PushedAuthorizationRequests_ReferenceValueHash",
            table: "PushedAuthorizationRequests",
            column: "ReferenceValueHash",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessionRequestIndices_RequestId",
            table: "SamlLogoutSessionRequestIndices",
            column: "RequestId",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessionRequestIndices_SamlLogoutSessionId",
            table: "SamlLogoutSessionRequestIndices",
            column: "SamlLogoutSessionId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessions_ExpiresAtUtc",
            table: "SamlLogoutSessions",
            column: "ExpiresAtUtc");

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessions_LogoutId",
            table: "SamlLogoutSessions",
            column: "LogoutId",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlSigninStates_ExpiresAtUtc",
            table: "SamlSigninStates",
            column: "ExpiresAtUtc");

        _ = migrationBuilder.CreateIndex(
            name: "IX_SamlSigninStates_StateId",
            table: "SamlSigninStates",
            column: "StateId",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ServerSideSessions_DisplayName",
            table: "ServerSideSessions",
            column: "DisplayName");

        _ = migrationBuilder.CreateIndex(
            name: "IX_ServerSideSessions_Expires",
            table: "ServerSideSessions",
            column: "Expires");

        _ = migrationBuilder.CreateIndex(
            name: "IX_ServerSideSessions_Key",
            table: "ServerSideSessions",
            column: "Key",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ServerSideSessions_SessionId",
            table: "ServerSideSessions",
            column: "SessionId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_ServerSideSessions_SubjectId",
            table: "ServerSideSessions",
            column: "SubjectId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(
            name: "DeviceCodes");

        _ = migrationBuilder.DropTable(
            name: "Keys");

        _ = migrationBuilder.DropTable(
            name: "PersistedGrants");

        _ = migrationBuilder.DropTable(
            name: "PushedAuthorizationRequests");

        _ = migrationBuilder.DropTable(
            name: "SamlLogoutSessionRequestIndices");

        _ = migrationBuilder.DropTable(
            name: "SamlSigninStates");

        _ = migrationBuilder.DropTable(
            name: "ServerSideSessions");

        _ = migrationBuilder.DropTable(
            name: "SamlLogoutSessions");
    }
}
