using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTrainerExperienceWithPaymentInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TeamJoinedDate",
                table: "Trainers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Trainers\" SET \"TeamJoinedDate\" = \"CreatedAt\" WHERE \"TeamJoinedDate\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "ExperienceYears",
                table: "Trainers");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Locations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlikPhoneNumber",
                table: "Locations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentDescription",
                table: "Locations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentRecipientName",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferTitleTemplate",
                table: "Locations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamJoinedDate",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "BlikPhoneNumber",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PaymentDescription",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PaymentRecipientName",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TransferTitleTemplate",
                table: "Locations");

            migrationBuilder.AddColumn<int>(
                name: "ExperienceYears",
                table: "Trainers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
