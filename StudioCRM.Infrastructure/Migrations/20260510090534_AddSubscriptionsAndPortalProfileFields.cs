using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsAndPortalProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ClientFiles\";");

            migrationBuilder.RenameIndex(
                name: "IX_ClientPackages_ClientId",
                table: "ClientPackages",
                newName: "IX_ClientPackages_OneActivePerClient");

            migrationBuilder.AddColumn<int>(
                name: "BillingType",
                table: "Packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionsPerWeek",
                table: "Packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveFolderId",
                table: "Clients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextPackageId",
                table: "Clients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalCancellationRequestedAt",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewalCancellationRequestedByUserId",
                table: "Clients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalCancelledAt",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewalCancelledByUserId",
                table: "Clients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubscriptionAutoRenewEnabled",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingPlanFileId",
                table: "Clients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingPlanFileName",
                table: "Clients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingPlanUrl",
                table: "Clients",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceApplied",
                table: "ClientPackages",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "ClientPackages",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PreviousClientPackageId",
                table: "ClientPackages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenewalSource",
                table: "ClientPackages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<int>(
                name: "SessionsPerWeek",
                table: "ClientPackages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ClientEmailChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    CurrentEmail = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    RequestedEmail = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientEmailChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientEmailChangeRequests_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_LocationId_BillingType_SessionsPerWeek_SessionsLim~",
                table: "Packages",
                columns: new[] { "LocationId", "BillingType", "SessionsPerWeek", "SessionsLimit" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientEmailChangeRequests_ClientId_Status",
                table: "ClientEmailChangeRequests",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_Locations_LocationId",
                table: "Packages",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Locations_LocationId",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "ClientEmailChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_Packages_LocationId_BillingType_SessionsPerWeek_SessionsLim~",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "BillingType",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "SessionsPerWeek",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "GoogleDriveFolderId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "NextPackageId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "RenewalCancellationRequestedAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "RenewalCancellationRequestedByUserId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "RenewalCancelledAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "RenewalCancelledByUserId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SubscriptionAutoRenewEnabled",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TrainingPlanFileId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TrainingPlanFileName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TrainingPlanUrl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "BalanceApplied",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "PreviousClientPackageId",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "RenewalSource",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "SessionsPerWeek",
                table: "ClientPackages");

            migrationBuilder.RenameIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages",
                newName: "IX_ClientPackages_ClientId");

            migrationBuilder.CreateTable(
                name: "ClientFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GoogleDriveFileId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsFolder = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisibleForClient = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientFiles_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientFiles_ClientId",
                table: "ClientFiles",
                column: "ClientId");
        }
    }
}
