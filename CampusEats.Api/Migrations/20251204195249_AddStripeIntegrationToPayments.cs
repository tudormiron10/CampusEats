using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusEats.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeIntegrationToPayments : Migration
    {
        private const string PaymentsTableName = "Payments";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientSecret",
                table: PaymentsTableName,
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: PaymentsTableName,
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "StripeEventId",
                table: PaymentsTableName,
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: PaymentsTableName,
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: PaymentsTableName,
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientSecret",
                table: PaymentsTableName);

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: PaymentsTableName);

            migrationBuilder.DropColumn(
                name: "StripeEventId",
                table: PaymentsTableName);

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: PaymentsTableName);

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: PaymentsTableName);
        }
    }
}
