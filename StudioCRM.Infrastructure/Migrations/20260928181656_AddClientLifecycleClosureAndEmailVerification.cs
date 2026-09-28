using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientLifecycleClosureAndEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Invitations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PortalAccessBlocked",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "ClientPackages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureDisposition",
                table: "ClientPackages",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "ClientPackages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "ClientPackages",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundConfirmedAt",
                table: "ClientPackages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                table: "ClientPackages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "ClientEmailChangeRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationExpiresAt",
                table: "ClientEmailChangeRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationTokenHash",
                table: "ClientEmailChangeRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "ClientEmailChangeRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientAuditEntries_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_ClientId",
                table: "Invitations",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAuditEntries_ClientId_CreatedAt",
                table: "ClientAuditEntries",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Clients_ClientId",
                table: "Invitations",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Clients_ClientId",
                table: "Invitations");

            migrationBuilder.DropTable(
                name: "ClientAuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_ClientId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "PortalAccessBlocked",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "ClosureDisposition",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "RefundConfirmedAt",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropColumn(
                name: "VerificationExpiresAt",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropColumn(
                name: "VerificationTokenHash",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "ClientEmailChangeRequests");
        }
    }
}
