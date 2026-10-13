using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodBasedPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages");

            migrationBuilder.AddColumn<int>(
                name: "BillingMode",
                table: "Packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BillingMode",
                table: "ClientPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PeriodAutoRenewEnabled",
                table: "ClientPackages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PeriodDurationDays",
                table: "ClientPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStartsAt",
                table: "ClientPackages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_BillingMode_IsActive_ValidUntil",
                table: "ClientPackages",
                columns: new[] { "BillingMode", "IsActive", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages",
                column: "ClientId",
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"ExpectedBillingType\" <> 5 AND \"BillingMode\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_PreviousClientPackageId",
                table: "ClientPackages",
                column: "PreviousClientPackageId",
                unique: true,
                filter: "\"BillingMode\" = 1 AND \"PreviousClientPackageId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientPackages_PeriodTerms",
                table: "ClientPackages",
                sql: "\"BillingMode\" <> 1 OR (\"TotalSessions\" = 0 AND \"UsedSessions\" = 0 AND \"PeriodDurationDays\" BETWEEN 1 AND 3660 AND \"PeriodStartsAt\" IS NOT NULL AND \"ValidUntil\" IS NOT NULL AND \"ValidUntil\" > \"PeriodStartsAt\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_BillingMode_IsActive_ValidUntil",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_PreviousClientPackageId",
                table: "ClientPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientPackages_PeriodTerms",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "BillingMode",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "BillingMode",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "PeriodAutoRenewEnabled",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "PeriodDurationDays",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "PeriodStartsAt",
                table: "ClientPackages");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages",
                column: "ClientId",
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"ExpectedBillingType\" <> 5");
        }
    }
}
