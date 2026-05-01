using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlookEventMappingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalendarEmail",
                table: "Locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttendeesJson",
                table: "ExternalCalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "ExternalCalendarEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocationEmail",
                table: "ExternalCalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MappingWarningsJson",
                table: "ExternalCalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesMasterId",
                table: "ExternalCalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CalendarEmail",
                table: "Locations",
                column: "CalendarEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_CalendarEmail",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CalendarEmail",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "AttendeesJson",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropColumn(
                name: "LocationEmail",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropColumn(
                name: "MappingWarningsJson",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropColumn(
                name: "SeriesMasterId",
                table: "ExternalCalendarEvents");
        }
    }
}
