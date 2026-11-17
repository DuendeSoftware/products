// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServerDb.Migrations.PersistedGrantDb;

/// <inheritdoc />
public partial class ServerSideSessionVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<int>(
            name: "Version",
            table: "ServerSideSessions",
            type: "int",
            nullable: false,
            defaultValue: 1);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
            name: "Version",
            table: "ServerSideSessions");
}
