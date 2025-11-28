using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusEats.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldDietaryTagsColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DietaryTags",
                table: "MenuItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DietaryTags",
                table: "MenuItems",
                type: "text",
                nullable: true);
        }
    }
}
