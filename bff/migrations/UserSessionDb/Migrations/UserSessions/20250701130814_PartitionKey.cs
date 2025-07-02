// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserSessionDb.Migrations.UserSessions;

/// <inheritdoc />
public partial class PartitionKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "ApplicationName",
            table: "UserSessions",
            newName: "PartitionKey");

        migrationBuilder.RenameIndex(
            name: "IX_UserSessions_ApplicationName_SubjectId_SessionId",
            table: "UserSessions",
            newName: "IX_UserSessions_PartitionKey_SubjectId_SessionId");

        migrationBuilder.RenameIndex(
            name: "IX_UserSessions_ApplicationName_SessionId",
            table: "UserSessions",
            newName: "IX_UserSessions_PartitionKey_SessionId");

        migrationBuilder.RenameIndex(
            name: "IX_UserSessions_ApplicationName_Key",
            table: "UserSessions",
            newName: "IX_UserSessions_PartitionKey_Key");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "PartitionKey",
            table: "UserSessions",
            newName: "ApplicationName");

        migrationBuilder.RenameIndex(
            name: "IX_UserSessions_PartitionKey_SubjectId_SessionId",
            table: "UserSessions",
            newName: "IX_UserSessions_ApplicationName_SubjectId_SessionId");

        migrationBuilder.RenameIndex(
            name: "IX_UserSessions_PartitionKey_SessionId",
            table: "UserSessions",
            newName: "IX_UserSessions_ApplicationName_SessionId");

        migrationBuilder.RenameIndex(
            name: "IX_UserSessions_PartitionKey_Key",
            table: "UserSessions",
            newName: "IX_UserSessions_ApplicationName_Key");
    }
}
