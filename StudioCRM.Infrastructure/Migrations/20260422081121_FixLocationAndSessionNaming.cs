using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    public partial class FixLocationAndSessionNaming : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Location",
                table: "Sessions",
                newName: "StudioRoom");

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Locations",
                columns: new[] { "Id", "Name", "City", "Address", "IsActive", "CreatedAt" },
                values: new object[,]
                {
                    { 1, "Niepołomice", "Niepołomice", "ul. Przykładowa 1", true, new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Kłaj", "Kłaj", "ul. Przykładowa 2", true, new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Clients",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "TrainerLocations",
                columns: table => new
                {
                    TrainerId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerLocations", x => new { x.TrainerId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_TrainerLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainerLocations_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Trainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE "Clients"
                SET "LocationId" = 1
                WHERE "LocationId" = 0;
            """);

            migrationBuilder.Sql("""
                UPDATE "Sessions"
                SET "LocationId" = 1
                WHERE "LocationId" = 0;
            """);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LocationId",
                table: "Clients",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LocationId",
                table: "Sessions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerLocations_LocationId",
                table: "TrainerLocations",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Locations_LocationId",
                table: "Clients",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Locations_LocationId",
                table: "Sessions",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Locations_LocationId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Locations_LocationId",
                table: "Sessions");

            migrationBuilder.DropTable(
                name: "TrainerLocations");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LocationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_LocationId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Sessions");

            migrationBuilder.RenameColumn(
                name: "StudioRoom",
                table: "Sessions",
                newName: "Location");
        }
    }
}