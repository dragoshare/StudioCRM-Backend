using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicGroupClassSignupFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages");

            migrationBuilder.AddColumn<bool>(
                name: "IsPubliclyBookable",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PublicCapacity",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "Sessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPubliclyAvailable",
                table: "Packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "Packages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Invitation");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PublicSlug",
                table: "Sessions",
                column: "PublicSlug");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PublicSlug",
                table: "Packages",
                column: "PublicSlug");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages",
                column: "ClientId",
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"ExpectedBillingType\" <> 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_PublicSlug",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Packages_PublicSlug",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "IsPubliclyBookable",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PublicCapacity",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IsPubliclyAvailable",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_OneActivePerClient",
                table: "ClientPackages",
                column: "ClientId",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }
    }
}
