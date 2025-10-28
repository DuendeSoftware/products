// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore.Migrations;

namespace Duende.Documentation.Mcp.Server.Database.Migrations;

/// <inheritdoc />
internal sealed partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"CREATE VIRTUAL TABLE FTSBlogArticle USING fts5(Id, Title, Content, tokenize = 'porter unicode61');");
        migrationBuilder.Sql(@"CREATE VIRTUAL TABLE FTSDocsArticle USING fts5(Id, Product, Title, Content, tokenize = 'porter unicode61');");
        migrationBuilder.Sql(@"CREATE VIRTUAL TABLE FTSSampleProject USING fts5(Id, Product, Title, Description, Files, tokenize = 'porter unicode61');");

        migrationBuilder.CreateTable(
            name: "State",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_State", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FTSBlogArticle");

        migrationBuilder.DropTable(
            name: "FTSDocsArticle");

        migrationBuilder.DropTable(
            name: "FTSSampleProject");

        migrationBuilder.DropTable(
            name: "State");
    }
}
