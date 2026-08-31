using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalEntityPaymentInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "LegalEntities",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlikPhoneNumber",
                table: "LegalEntities",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentDescription",
                table: "LegalEntities",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentRecipientName",
                table: "LegalEntities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferTitleTemplate",
                table: "LegalEntities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "LegalEntities" le
                SET
                    "BankAccountNumber" = COALESCE(
                        le."BankAccountNumber",
                        (
                            SELECT NULLIF(BTRIM(l."BankAccountNumber"), '')
                            FROM "Locations" l
                            WHERE l."LegalEntityId" = le."Id"
                                AND NULLIF(BTRIM(l."BankAccountNumber"), '') IS NOT NULL
                            ORDER BY l."Id"
                            LIMIT 1
                        )),
                    "BlikPhoneNumber" = COALESCE(
                        le."BlikPhoneNumber",
                        (
                            SELECT NULLIF(BTRIM(l."BlikPhoneNumber"), '')
                            FROM "Locations" l
                            WHERE l."LegalEntityId" = le."Id"
                                AND NULLIF(BTRIM(l."BlikPhoneNumber"), '') IS NOT NULL
                            ORDER BY l."Id"
                            LIMIT 1
                        )),
                    "PaymentDescription" = COALESCE(
                        le."PaymentDescription",
                        (
                            SELECT NULLIF(BTRIM(l."PaymentDescription"), '')
                            FROM "Locations" l
                            WHERE l."LegalEntityId" = le."Id"
                                AND NULLIF(BTRIM(l."PaymentDescription"), '') IS NOT NULL
                            ORDER BY l."Id"
                            LIMIT 1
                        )),
                    "PaymentRecipientName" = COALESCE(
                        le."PaymentRecipientName",
                        (
                            SELECT NULLIF(BTRIM(l."PaymentRecipientName"), '')
                            FROM "Locations" l
                            WHERE l."LegalEntityId" = le."Id"
                                AND NULLIF(BTRIM(l."PaymentRecipientName"), '') IS NOT NULL
                            ORDER BY l."Id"
                            LIMIT 1
                        )),
                    "TransferTitleTemplate" = COALESCE(
                        le."TransferTitleTemplate",
                        (
                            SELECT NULLIF(BTRIM(l."TransferTitleTemplate"), '')
                            FROM "Locations" l
                            WHERE l."LegalEntityId" = le."Id"
                                AND NULLIF(BTRIM(l."TransferTitleTemplate"), '') IS NOT NULL
                            ORDER BY l."Id"
                            LIMIT 1
                        ))
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "BlikPhoneNumber",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "PaymentDescription",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "PaymentRecipientName",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "TransferTitleTemplate",
                table: "LegalEntities");
        }
    }
}
