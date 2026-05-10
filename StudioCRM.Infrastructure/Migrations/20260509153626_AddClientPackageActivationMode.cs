using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPackageActivationMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "ClientPackages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivatedByUserId",
                table: "ClientPackages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivationMode",
                table: "ClientPackages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RequestedByUserId",
                table: "ClientPackages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_ClientId",
                table: "ClientPackages",
                column: "ClientId",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_ClientId",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "ActivatedByUserId",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "ActivationMode",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "ClientPackages");
        }
    }
}
