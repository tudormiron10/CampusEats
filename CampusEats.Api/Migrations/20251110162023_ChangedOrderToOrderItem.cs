using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusEats.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangedOrderToOrderItem : Migration
    {
        private const string MenuItemIdColumnName = "MenuItemId";
        private const string OrdersTableName = "Orders";
        private const string OrderItemsTableName = "OrderItems";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemOrder");

            migrationBuilder.AddColumn<Guid>(
                name: MenuItemIdColumnName,
                table: OrdersTableName,
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: OrderItemsTableName,
                columns: table => new
                {
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.OrderItemId);
                    table.ForeignKey(
                        name: "FK_OrderItems_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: MenuItemIdColumnName,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: OrdersTableName,
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MenuItemId",
                table: OrdersTableName,
                column: MenuItemIdColumnName);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_MenuItemId",
                table: OrderItemsTableName,
                column: MenuItemIdColumnName);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: OrderItemsTableName,
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_MenuItems_MenuItemId",
                table: OrdersTableName,
                column: MenuItemIdColumnName,
                principalTable: "MenuItems",
                principalColumn: MenuItemIdColumnName);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_MenuItems_MenuItemId",
                table: OrdersTableName);

            migrationBuilder.DropTable(
                name: OrderItemsTableName);

            migrationBuilder.DropIndex(
                name: "IX_Orders_MenuItemId",
                table: OrdersTableName);

            migrationBuilder.DropColumn(
                name: MenuItemIdColumnName,
                table: OrdersTableName);

            migrationBuilder.CreateTable(
                name: "MenuItemOrder",
                columns: table => new
                {
                    ItemsMenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdersOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemOrder", x => new { x.ItemsMenuItemId, x.OrdersOrderId });
                    table.ForeignKey(
                        name: "FK_MenuItemOrder_MenuItems_ItemsMenuItemId",
                        column: x => x.ItemsMenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: MenuItemIdColumnName,
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemOrder_Orders_OrdersOrderId",
                        column: x => x.OrdersOrderId,
                        principalTable: OrdersTableName,
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOrder_OrdersOrderId",
                table: "MenuItemOrder",
                column: "OrdersOrderId");
        }
    }
}
