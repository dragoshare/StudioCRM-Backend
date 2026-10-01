using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformOrganizationRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UiVariant = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BrandingProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_OrganizationBrandingProfiles_BrandingProfileId",
                        column: x => x.BrandingProfileId,
                        principalTable: "OrganizationBrandingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Organizations",
                columns: new[] { "Id", "BrandingProfileId", "Name", "Slug", "UiVariant" },
                values: new object[] { new Guid("b5100000-0000-4000-8000-000000000002"), new Guid("b5100000-0000-4000-8000-000000000001"), "BSworkout", "bsworkout", "bsworkout" });

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_BrandingProfileId",
                table: "Organizations",
                column: "BrandingProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
