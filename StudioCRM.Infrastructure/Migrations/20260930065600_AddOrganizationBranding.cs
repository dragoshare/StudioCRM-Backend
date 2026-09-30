using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationBrandingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    DraftJson = table.Column<string>(type: "text", nullable: false),
                    PublishedJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBrandingProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationBrandingAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBrandingAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationBrandingAssets_OrganizationBrandingProfiles_Pro~",
                        column: x => x.ProfileId,
                        principalTable: "OrganizationBrandingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationBrandingAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBrandingAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationBrandingAudits_OrganizationBrandingProfiles_Pro~",
                        column: x => x.ProfileId,
                        principalTable: "OrganizationBrandingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationBrandingVersions",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBrandingVersions", x => new { x.ProfileId, x.Version });
                    table.ForeignKey(
                        name: "FK_OrganizationBrandingVersions_OrganizationBrandingProfiles_P~",
                        column: x => x.ProfileId,
                        principalTable: "OrganizationBrandingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "OrganizationBrandingProfiles",
                columns: new[] { "Id", "DraftJson", "Name", "PublishedJson", "PublishedVersion", "Revision" },
                values: new object[] { new Guid("b5100000-0000-4000-8000-000000000001"), "{}", "BSworkout", null, 0, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBrandingAssets_ProfileId_Id",
                table: "OrganizationBrandingAssets",
                columns: new[] { "ProfileId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBrandingAudits_ProfileId_Id",
                table: "OrganizationBrandingAudits",
                columns: new[] { "ProfileId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationBrandingAssets");

            migrationBuilder.DropTable(
                name: "OrganizationBrandingAudits");

            migrationBuilder.DropTable(
                name: "OrganizationBrandingVersions");

            migrationBuilder.DropTable(
                name: "OrganizationBrandingProfiles");
        }
    }
}
