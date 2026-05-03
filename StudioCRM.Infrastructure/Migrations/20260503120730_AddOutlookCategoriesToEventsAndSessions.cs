using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlookCategoriesToEventsAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OutlookCategoriesJson",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryOutlookCategory",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoriesJson",
                table: "ExternalCalendarEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OutlookCategoriesJson",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PrimaryOutlookCategory",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CategoriesJson",
                table: "ExternalCalendarEvents");
        }
    }
}
