using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientLocationMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientLocationMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    IsHomeLocation = table.Column<bool>(type: "boolean", nullable: false),
                    GroupAccessEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientLocationMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientLocationMemberships_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientLocationMemberships_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLocationMemberships_ClientId_LocationId",
                table: "ClientLocationMemberships",
                columns: new[] { "ClientId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientLocationMemberships_LocationId",
                table: "ClientLocationMemberships",
                column: "LocationId");

            migrationBuilder.Sql(
                """
                INSERT INTO "ClientLocationMemberships"
                    ("ClientId", "LocationId", "IsHomeLocation", "GroupAccessEnabled", "Source", "JoinedAt", "UpdatedAt")
                SELECT
                    c."Id", c."LocationId", TRUE, FALSE, 'Migration', c."CreatedAt", NOW()
                FROM "Clients" c
                ON CONFLICT ("ClientId", "LocationId") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "ClientLocationMemberships"
                    ("ClientId", "LocationId", "IsHomeLocation", "GroupAccessEnabled", "Source", "JoinedAt", "UpdatedAt")
                SELECT DISTINCT
                    cp."ClientId", cp."LocationId", FALSE, TRUE, 'MigrationGroupPackage', cp."PurchaseDate", NOW()
                FROM "ClientPackages" cp
                WHERE cp."ExpectedBillingType" = 5
                  AND cp."LocationId" IS NOT NULL
                ON CONFLICT ("ClientId", "LocationId") DO UPDATE
                SET "GroupAccessEnabled" = TRUE,
                    "UpdatedAt" = NOW();
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ClientPackages"
                SET "IsActive" = FALSE,
                    "ActivatedAt" = NULL,
                    "ActivatedByUserId" = NULL,
                    "ValidUntil" = NULL
                WHERE "ExpectedBillingType" = 5
                  AND "PaymentStatus" <> 1
                  AND "RenewalSource" = 'GroupPublic';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientLocationMemberships");
        }
    }
}
