using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusEats.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDietaryTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DietaryTags",
                columns: table => new
                {
                    DietaryTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryTags", x => x.DietaryTagId);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemDietaryTags",
                columns: table => new
                {
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryTagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemDietaryTags", x => new { x.MenuItemId, x.DietaryTagId });
                    table.ForeignKey(
                        name: "FK_MenuItemDietaryTags_DietaryTags_DietaryTagId",
                        column: x => x.DietaryTagId,
                        principalTable: "DietaryTags",
                        principalColumn: "DietaryTagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemDietaryTags_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemDietaryTags_DietaryTagId",
                table: "MenuItemDietaryTags",
                column: "DietaryTagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemDietaryTags");

            migrationBuilder.DropTable(
                name: "DietaryTags");
        }
    }
}
