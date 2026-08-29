using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProviderSettlementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ProviderFeeAmount",
                table: "ClientPayments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderNetAmount",
                table: "ClientPayments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderPayoutDate",
                table: "ClientPayments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderSettledAt",
                table: "ClientPayments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSettlementId",
                table: "ClientPayments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ProviderPayoutDate",
                table: "ClientPayments",
                column: "ProviderPayoutDate");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ProviderSettlementId",
                table: "ClientPayments",
                column: "ProviderSettlementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_ProviderPayoutDate",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_ProviderSettlementId",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ProviderFeeAmount",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ProviderNetAmount",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ProviderPayoutDate",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ProviderSettledAt",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ProviderSettlementId",
                table: "ClientPayments");
        }
    }
}
