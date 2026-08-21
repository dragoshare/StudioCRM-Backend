using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepairInvitationSendTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Invitations"
                ADD COLUMN IF NOT EXISTS "TrainerId" integer;

                ALTER TABLE "Invitations"
                ADD COLUMN IF NOT EXISTS "LastSendError" character varying(2000);

                ALTER TABLE "Invitations"
                ADD COLUMN IF NOT EXISTS "LastSentAt" timestamp with time zone;

                CREATE INDEX IF NOT EXISTS "IX_Invitations_TrainerId"
                ON "Invitations" ("TrainerId");

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_Invitations_Trainers_TrainerId'
                            AND conrelid = '"Invitations"'::regclass
                    ) THEN
                        ALTER TABLE "Invitations"
                        ADD CONSTRAINT "FK_Invitations_Trainers_TrainerId"
                        FOREIGN KEY ("TrainerId")
                        REFERENCES "Trainers" ("Id")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Invitations"
                DROP CONSTRAINT IF EXISTS "FK_Invitations_Trainers_TrainerId";

                DROP INDEX IF EXISTS "IX_Invitations_TrainerId";

                ALTER TABLE "Invitations"
                DROP COLUMN IF EXISTS "LastSentAt";

                ALTER TABLE "Invitations"
                DROP COLUMN IF EXISTS "LastSendError";

                ALTER TABLE "Invitations"
                DROP COLUMN IF EXISTS "TrainerId";
                """);
        }
    }
}
