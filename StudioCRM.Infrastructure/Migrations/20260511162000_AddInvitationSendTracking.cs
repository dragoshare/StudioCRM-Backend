using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    [Migration("20260511162000_AddInvitationSendTracking")]
    public partial class AddInvitationSendTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSendError",
                table: "Invitations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSentAt",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSendError",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "LastSentAt",
                table: "Invitations");
        }
    }
}
