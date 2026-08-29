using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneRewardClaimUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RewardClaimedByUserId",
                table: "ClientMilestones",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_RewardClaimedByUserId",
                table: "ClientMilestones",
                column: "RewardClaimedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientMilestones_Users_RewardClaimedByUserId",
                table: "ClientMilestones",
                column: "RewardClaimedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientMilestones_Users_RewardClaimedByUserId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_RewardClaimedByUserId",
                table: "ClientMilestones");

            migrationBuilder.DropColumn(
                name: "RewardClaimedByUserId",
                table: "ClientMilestones");
        }
    }
}
