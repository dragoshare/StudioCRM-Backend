using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingStartDateToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TrainingStartDate",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MilestoneDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RequiredMonths = table.Column<int>(type: "integer", nullable: false),
                    RewardName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    MilestoneDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    AchievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRewardClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    RewardClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardClaimedByTrainerId = table.Column<int>(type: "integer", nullable: true),
                    RewardClaimNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientMilestones_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientMilestones_MilestoneDefinitions_MilestoneDefinitionId",
                        column: x => x.MilestoneDefinitionId,
                        principalTable: "MilestoneDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientMilestones_Trainers_RewardClaimedByTrainerId",
                        column: x => x.RewardClaimedByTrainerId,
                        principalTable: "Trainers",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "MilestoneDefinitions",
                columns: new[] { "Id", "Description", "IsActive", "Name", "RequiredMonths", "RewardName" },
                values: new object[,]
                {
                    { 1, "Nagroda za regularne uczęszczanie przez 3 miesiące.", true, "3 miesiące treningów", 3, "Mały upominek" },
                    { 2, "Nagroda za regularne uczęszczanie przez 6 miesięcy.", true, "6 miesięcy treningów", 6, "Większy upominek" },
                    { 3, "Nagroda za rok treningów w studio.", true, "12 miesięcy treningów", 12, "Koszulka z logo studia" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_ClientId",
                table: "ClientMilestones",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_MilestoneDefinitionId",
                table: "ClientMilestones",
                column: "MilestoneDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_RewardClaimedByTrainerId",
                table: "ClientMilestones",
                column: "RewardClaimedByTrainerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientMilestones");

            migrationBuilder.DropTable(
                name: "MilestoneDefinitions");

            migrationBuilder.DropColumn(
                name: "TrainingStartDate",
                table: "Clients");
        }
    }
}
