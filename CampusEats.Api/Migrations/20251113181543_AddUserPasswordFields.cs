using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusEats.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasswordFields : Migration
    {
        private const string UsersTableName = "Users";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PasswordHash",
                table: UsersTableName,
                type: "bytea",
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<byte[]>(
                name: "PasswordSalt",
                table: UsersTableName,
                type: "bytea",
                nullable: false,
                defaultValue: Array.Empty<byte>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: UsersTableName);

            migrationBuilder.DropColumn(
                name: "PasswordSalt",
                table: UsersTableName);
        }
    }
}
