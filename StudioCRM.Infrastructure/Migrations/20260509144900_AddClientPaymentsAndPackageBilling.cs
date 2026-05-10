using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPaymentsAndPackageBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "ClientPackages",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ClientPackages",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "PLN");

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "ClientPackages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsedSessions",
                table: "ClientPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                INSERT INTO "ClientPackages"
                    ("ClientId", "PackageId", "Name", "TotalSessions", "UsedSessions", "TotalPrice", "AmountPaid",
                     "ExpectedUnitPrice", "Currency", "LocationId", "ExpectedBillingType", "PaymentStatus",
                     "PurchaseDate", "ValidUntil", "PaidAt", "PaymentDueDate", "IsActive")
                SELECT
                    c."Id",
                    p."Id",
                    p."Name",
                    p."SessionsLimit",
                    0,
                    p."Price",
                    CASE WHEN c."BillingStatus" = 'Paid' THEN p."Price" ELSE 0 END,
                    CASE WHEN p."SessionsLimit" = 0 THEN p."Price" ELSE p."Price" / p."SessionsLimit" END,
                    COALESCE(NULLIF(p."Currency", ''), 'PLN'),
                    c."LocationId",
                    CASE
                        WHEN p."Name" ILIKE '%4:1%' THEN 4
                        WHEN p."Name" ILIKE '%3:1%' THEN 3
                        WHEN p."Name" ILIKE '%2:1%' THEN 2
                        ELSE 1
                    END,
                    CASE
                        WHEN c."BillingStatus" = 'Paid' THEN 1
                        WHEN c."BillingStatus" = 'Overdue' THEN 2
                        ELSE 0
                    END,
                    COALESCE(c."CreatedAt", NOW()),
                    CASE
                        WHEN p."DurationDays" > 0 THEN COALESCE(c."CreatedAt", NOW()) + (p."DurationDays" || ' days')::interval
                        ELSE NULL
                    END,
                    CASE WHEN c."BillingStatus" = 'Paid' THEN NOW() ELSE NULL END,
                    CASE WHEN c."BillingStatus" = 'Paid' THEN NULL ELSE NOW() + interval '7 days' END,
                    TRUE
                FROM "Clients" c
                JOIN "Packages" p ON p."Id" = c."ActivePackageId"
                WHERE c."ActivePackageId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "ClientPackages" cp
                      WHERE cp."ClientId" = c."Id"
                        AND cp."PackageId" = p."Id"
                        AND cp."IsActive" = TRUE
                  );
                """);

            migrationBuilder.CreateTable(
                name: "ClientPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    ClientPackageId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalPaymentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPayments_ClientPackages_ClientPackageId",
                        column: x => x.ClientPackageId,
                        principalTable: "ClientPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientPayments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_LocationId",
                table: "ClientPackages",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ClientId",
                table: "ClientPayments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ClientPackageId",
                table: "ClientPayments",
                column: "ClientPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_Status",
                table: "ClientPayments",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPackages_Locations_LocationId",
                table: "ClientPackages",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientPackages_Locations_LocationId",
                table: "ClientPackages");

            migrationBuilder.DropTable(
                name: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_LocationId",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "UsedSessions",
                table: "ClientPackages");
        }
    }
}
