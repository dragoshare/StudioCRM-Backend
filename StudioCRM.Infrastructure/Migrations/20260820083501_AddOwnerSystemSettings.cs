using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "CreatedAt", "Key", "UpdatedAt", "UpdatedByUserId", "Value" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "DefaultPackageValidityDays", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), null, "45" },
                    { 2, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "DefaultSessionDurationMinutes", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), null, "60" },
                    { 3, new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), "DefaultPaymentDueDays", new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc), null, "7" }
                });

            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"SystemSettings"', 'Id'), (SELECT MAX("Id") FROM "SystemSettings"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
