using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreedSessionTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgreedSessionTermsJson",
                table: "ClientPackages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreedSessionTermsJson",
                table: "ClientPackages");
        }
    }
}
