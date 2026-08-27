using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFiscalCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FiscalReceiptMode",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "FiscalRegisterName",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalRegisterNumber",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalEntityId",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Locations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckoutExpiresAt",
                table: "ClientPayments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "ClientPayments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalEntityId",
                table: "ClientPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "ClientPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentProvider",
                table: "ClientPayments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentProviderAccountId",
                table: "ClientPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentId",
                table: "ClientPayments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                table: "ClientPayments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptIssuedByUserId",
                table: "ClientPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNote",
                table: "ClientPayments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptRequired",
                table: "ClientPayments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptSentAt",
                table: "ClientPayments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WebhookReceivedAt",
                table: "ClientPayments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LegalEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentProviderAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LegalEntityId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MerchantId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PosId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    WebhookSecretConfigured = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProviderAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentProviderAccounts_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentProviderAccounts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LegalEntityId",
                table: "Locations",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_LegalEntityId",
                table: "ClientPayments",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_LocationId",
                table: "ClientPayments",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_PaymentProviderAccountId",
                table: "ClientPayments",
                column: "PaymentProviderAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ProviderPaymentId",
                table: "ClientPayments",
                column: "ProviderPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ReceiptStatus",
                table: "ClientPayments",
                column: "ReceiptStatus");

            migrationBuilder.CreateIndex(
                name: "IX_LegalEntities_Nip",
                table: "LegalEntities",
                column: "Nip");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderAccounts_LegalEntityId_Provider_IsActive",
                table: "PaymentProviderAccounts",
                columns: new[] { "LegalEntityId", "Provider", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderAccounts_LocationId_Provider_IsActive",
                table: "PaymentProviderAccounts",
                columns: new[] { "LocationId", "Provider", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_LegalEntities_LegalEntityId",
                table: "ClientPayments",
                column: "LegalEntityId",
                principalTable: "LegalEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_Locations_LocationId",
                table: "ClientPayments",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_PaymentProviderAccounts_PaymentProviderAccou~",
                table: "ClientPayments",
                column: "PaymentProviderAccountId",
                principalTable: "PaymentProviderAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_LegalEntities_LegalEntityId",
                table: "Locations",
                column: "LegalEntityId",
                principalTable: "LegalEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_LegalEntities_LegalEntityId",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_Locations_LocationId",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_PaymentProviderAccounts_PaymentProviderAccou~",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_LegalEntities_LegalEntityId",
                table: "Locations");

            migrationBuilder.DropTable(
                name: "PaymentProviderAccounts");

            migrationBuilder.DropTable(
                name: "LegalEntities");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LegalEntityId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_LegalEntityId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_LocationId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_PaymentProviderAccountId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_ProviderPaymentId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_ReceiptStatus",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "FiscalReceiptMode",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "FiscalRegisterName",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "FiscalRegisterNumber",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "LegalEntityId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CheckoutExpiresAt",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "LegalEntityId",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "PaymentProvider",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "PaymentProviderAccountId",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentId",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReceiptIssuedByUserId",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReceiptNote",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReceiptRequired",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "ReceiptSentAt",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "WebhookReceivedAt",
                table: "ClientPayments");
        }
    }
}
