using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringSessionSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInstanceNumber",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurringGroupId",
                table: "Sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RecurringGroupId",
                table: "Sessions",
                column: "RecurringGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_RecurringGroupId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecurrenceInstanceNumber",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecurringGroupId",
                table: "Sessions");
        }
    }
}
