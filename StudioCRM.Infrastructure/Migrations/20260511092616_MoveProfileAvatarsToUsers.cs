using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveProfileAvatarsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Users" AS u
                SET "AvatarUrl" = COALESCE(NULLIF(u."AvatarUrl", ''), t."AvatarUrl")
                FROM "Trainers" AS t
                WHERE t."UserId" = u."Id"
                  AND t."AvatarUrl" IS NOT NULL
                  AND t."AvatarUrl" <> '';
                """);

            migrationBuilder.Sql("""
                UPDATE "Users" AS u
                SET "AvatarUrl" = COALESCE(NULLIF(u."AvatarUrl", ''), c."AvatarUrl")
                FROM "Clients" AS c
                WHERE c."UserId" = u."Id"
                  AND c."AvatarUrl" IS NOT NULL
                  AND c."AvatarUrl" <> '';
                """);

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Trainers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Clients",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Trainers" AS t
                SET "AvatarUrl" = u."AvatarUrl"
                FROM "Users" AS u
                WHERE t."UserId" = u."Id"
                  AND u."AvatarUrl" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "Clients" AS c
                SET "AvatarUrl" = u."AvatarUrl"
                FROM "Users" AS u
                WHERE c."UserId" = u."Id"
                  AND u."AvatarUrl" IS NOT NULL;
                """);
        }
    }
}
