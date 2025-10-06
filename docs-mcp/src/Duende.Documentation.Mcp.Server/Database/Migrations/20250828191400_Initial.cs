// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duende.Documentation.Mcp.Server.Database.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // migrationBuilder.CreateTable(
        //     name: "FTSBlogArticle",
        //     columns: table => new
        //     {
        //         Id = table.Column<string>(type: "TEXT", nullable: false),
        //         Title = table.Column<string>(type: "TEXT", nullable: false),
        //         Content = table.Column<string>(type: "TEXT", nullable: false)
        //     },
        //     constraints: table =>
        //     {
        //         table.PrimaryKey("PK_FTSBlogArticle", x => x.Id);
        //     });
        //
        // migrationBuilder.CreateTable(
        //     name: "FTSDocsArticle",
        //     columns: table => new
        //     {
        //         Id = table.Column<string>(type: "TEXT", nullable: false),
        //         Product = table.Column<string>(type: "TEXT", nullable: false),
        //         Title = table.Column<string>(type: "TEXT", nullable: false),
        //         Content = table.Column<string>(type: "TEXT", nullable: false)
        //     },
        //     constraints: table =>
        //     {
        //         table.PrimaryKey("PK_FTSDocsArticle", x => x.Id);
        //     });
        //
        // migrationBuilder.CreateTable(
        //     name: "FTSSampleProject",
        //     columns: table => new
        //     {
        //         Id = table.Column<string>(type: "TEXT", nullable: false),
        //         Product = table.Column<string>(type: "TEXT", nullable: false),
        //         Title = table.Column<string>(type: "TEXT", nullable: false),
        //         Description = table.Column<string>(type: "TEXT", nullable: false),
        //         Files = table.Column<string>(type: "TEXT", nullable: false)
        //     },
        //     constraints: table =>
        //     {
        //         table.PrimaryKey("PK_FTSSampleProject", x => x.Id);
        //     });

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
