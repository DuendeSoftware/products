// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServerDb.Migrations.PersistedGrantDb;

/// <inheritdoc />
public partial class AddSamlOperationalTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SamlLogoutSessions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                LogoutId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SerializedSession = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SamlLogoutSessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SamlSigninStates",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                StateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SerializedState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ServiceProviderEntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SamlSigninStates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SamlLogoutSessionRequestIndices",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SamlLogoutSessionId = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SamlLogoutSessionRequestIndices", x => x.Id);
                table.ForeignKey(
                    name: "FK_SamlLogoutSessionRequestIndices_SamlLogoutSessions_SamlLogoutSessionId",
                    column: x => x.SamlLogoutSessionId,
                    principalTable: "SamlLogoutSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessionRequestIndices_RequestId",
            table: "SamlLogoutSessionRequestIndices",
            column: "RequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessionRequestIndices_SamlLogoutSessionId",
            table: "SamlLogoutSessionRequestIndices",
            column: "SamlLogoutSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessions_ExpiresAtUtc",
            table: "SamlLogoutSessions",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_SamlLogoutSessions_LogoutId",
            table: "SamlLogoutSessions",
            column: "LogoutId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SamlSigninStates_ExpiresAtUtc",
            table: "SamlSigninStates",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_SamlSigninStates_StateId",
            table: "SamlSigninStates",
            column: "StateId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SamlLogoutSessionRequestIndices");

        migrationBuilder.DropTable(
            name: "SamlSigninStates");

        migrationBuilder.DropTable(
            name: "SamlLogoutSessions");
    }
}
