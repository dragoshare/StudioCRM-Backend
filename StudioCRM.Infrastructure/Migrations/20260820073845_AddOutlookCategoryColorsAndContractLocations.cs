using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlookCategoryColorsAndContractLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OutlookCategoryColorsJson",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryColorsJson",
                table: "ExternalCalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrainerContractLocations",
                columns: table => new
                {
                    TrainerContractId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerContractLocations", x => new { x.TrainerContractId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_TrainerContractLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainerContractLocations_TrainerContracts_TrainerContractId",
                        column: x => x.TrainerContractId,
                        principalTable: "TrainerContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "TrainerContractLocations" ("TrainerContractId", "LocationId")
                SELECT c."Id", tl."LocationId"
                FROM "TrainerContracts" c
                INNER JOIN "TrainerLocations" tl ON tl."TrainerId" = c."TrainerId"
                ON CONFLICT ("TrainerContractId", "LocationId") DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TrainerContractLocations_LocationId",
                table: "TrainerContractLocations",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainerContractLocations");

            migrationBuilder.DropColumn(
                name: "OutlookCategoryColorsJson",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CategoryColorsJson",
                table: "ExternalCalendarEvents");
        }
    }
}
