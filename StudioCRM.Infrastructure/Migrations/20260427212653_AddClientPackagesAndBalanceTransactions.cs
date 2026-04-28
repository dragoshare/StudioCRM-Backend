using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPackagesAndBalanceTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualBillingType",
                table: "SessionParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualUnitPrice",
                table: "SessionParticipants",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceDifference",
                table: "SessionParticipants",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientPackageId",
                table: "SessionParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedUnitPrice",
                table: "SessionParticipants",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCountedFromPackage",
                table: "SessionParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlannedBillingType",
                table: "SessionParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    PackageId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedBillingType = table.Column<int>(type: "integer", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPackages_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientPackages_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientBalanceTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    ClientPackageId = table.Column<int>(type: "integer", nullable: true),
                    SessionId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientBalanceTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientBalanceTransactions_ClientPackages_ClientPackageId",
                        column: x => x.ClientPackageId,
                        principalTable: "ClientPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientBalanceTransactions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientBalanceTransactions_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_ClientPackageId",
                table: "SessionParticipants",
                column: "ClientPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBalanceTransactions_ClientId",
                table: "ClientBalanceTransactions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBalanceTransactions_ClientPackageId",
                table: "ClientBalanceTransactions",
                column: "ClientPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBalanceTransactions_SessionId",
                table: "ClientBalanceTransactions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_ClientId_IsActive",
                table: "ClientPackages",
                columns: new[] { "ClientId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_PackageId",
                table: "ClientPackages",
                column: "PackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_ClientPackages_ClientPackageId",
                table: "SessionParticipants",
                column: "ClientPackageId",
                principalTable: "ClientPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_ClientPackages_ClientPackageId",
                table: "SessionParticipants");

            migrationBuilder.DropTable(
                name: "ClientBalanceTransactions");

            migrationBuilder.DropTable(
                name: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_ClientPackageId",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "ActualBillingType",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "ActualUnitPrice",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "BalanceDifference",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "ClientPackageId",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "ExpectedUnitPrice",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "IsCountedFromPackage",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "PlannedBillingType",
                table: "SessionParticipants");
        }
    }
}
