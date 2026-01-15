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
        private const string OrdersTableName = "Orders";
        private const string UsersTableName = "Users";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: ForeignKeyName,
                table: OrdersTableName);

            migrationBuilder.AlterColumn<Guid>(
                name: UserIdColumnName,
                table: OrdersTableName,
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: ForeignKeyName,
                table: OrdersTableName,
                column: UserIdColumnName,
                principalTable: UsersTableName,
                principalColumn: UserIdColumnName,
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: ForeignKeyName,
                table: OrdersTableName);

            migrationBuilder.AlterColumn<Guid>(
                name: UserIdColumnName,
                table: OrdersTableName,
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: ForeignKeyName,
                table: OrdersTableName,
                column: UserIdColumnName,
                principalTable: UsersTableName,
                principalColumn: UserIdColumnName,
                onDelete: ReferentialAction.Cascade);
        }
    }
}
