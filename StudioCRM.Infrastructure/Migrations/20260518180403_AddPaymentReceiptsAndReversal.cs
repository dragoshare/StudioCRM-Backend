using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReceiptsAndReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptIssuedAt",
                table: "ClientPayments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                table: "ClientPayments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptStatus",
                table: "ClientPayments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "ClientPayments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAt",
                table: "ClientPayments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversedByUserId",
                table: "ClientPayments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptIssuedAt",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReceiptStatus",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "ClientPayments");
        }
    }
}
