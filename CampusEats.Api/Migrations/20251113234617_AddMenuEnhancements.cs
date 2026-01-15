using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusEats.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuEnhancements : Migration
    {
        private const string MenuItemsTableName = "MenuItems";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: MenuItemsTableName);

            migrationBuilder.AddColumn<string>(
                name: "DietaryTags",
                table: MenuItemsTableName,
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: MenuItemsTableName,
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: MenuItemsTableName,
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: MenuItemsTableName,
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropColumn(
                name: "DietaryTags",
                table: MenuItemsTableName);

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: MenuItemsTableName);

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: MenuItemsTableName);

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: MenuItemsTableName);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: MenuItemsTableName,
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
