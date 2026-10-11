using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalReadIndexesAndPaymentIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_LocationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_TrainerId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Clients_TrainerId",
                table: "Clients");

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "ClientPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "ClientPayments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LocationId_StartAt",
                table: "Sessions",
                columns: new[] { "LocationId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TrainerId_StartAt",
                table: "Sessions",
                columns: new[] { "TrainerId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutlookOAuthChallenges_ExpiresAt",
                table: "OutlookOAuthChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TrainerId_LastName_FirstName_Id",
                table: "Clients",
                columns: new[] { "TrainerId", "LastName", "FirstName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ClientId_CreatedByUserId_Source_IdempotencyK~",
                table: "ClientPayments",
                columns: new[] { "ClientId", "CreatedByUserId", "Source", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_Status_PaymentDate_Id",
                table: "ClientPayments",
                columns: new[] { "Status", "PaymentDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_LocationId_StartAt",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_TrainerId_StartAt",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_OutlookOAuthChallenges_ExpiresAt",
                table: "OutlookOAuthChallenges");

            migrationBuilder.DropIndex(
                name: "IX_Clients_TrainerId_LastName_FirstName_Id",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_ClientId_CreatedByUserId_Source_IdempotencyK~",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_Status_PaymentDate_Id",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "ClientPayments");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LocationId",
                table: "Sessions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TrainerId",
                table: "Sessions",
                column: "TrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TrainerId",
                table: "Clients",
                column: "TrainerId");
        }
    }
}
