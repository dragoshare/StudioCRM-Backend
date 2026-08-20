using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationTrainerAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrainerId",
                table: "Invitations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TrainerId",
                table: "Invitations",
                column: "TrainerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Trainers_TrainerId",
                table: "Invitations",
                column: "TrainerId",
                principalTable: "Trainers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Trainers_TrainerId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_TrainerId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "TrainerId",
                table: "Invitations");
        }
    }
}
