using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusEats.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeOrderUserIdNullable : Migration
    {
        private const string ForeignKeyName = "FK_Orders_Users_UserId";
        private const string UserIdColumnName = "UserId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: ForeignKeyName,
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: UserIdColumnName,
                table: "Orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: ForeignKeyName,
                table: "Orders",
                column: UserIdColumnName,
                principalTable: "Users",
                principalColumn: UserIdColumnName,
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: ForeignKeyName,
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: UserIdColumnName,
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: ForeignKeyName,
                table: "Orders",
                column: UserIdColumnName,
                principalTable: "Users",
                principalColumn: UserIdColumnName,
                onDelete: ReferentialAction.Cascade);
        }
    }
}
