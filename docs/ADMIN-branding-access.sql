-- Run only via a trusted database administrator connection.
-- Replace 0 with the reviewed existing user ID; default cannot grant access.
-- This script never creates an account, password or Owner role.
BEGIN;
DO $$
DECLARE target_id integer := 0;
DECLARE admin_role integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id" = target_id AND "IsActive" AND "EmailVerifiedAt" IS NOT NULL) THEN
        RAISE EXCEPTION 'Select an active, email-verified administrator account first';
    END IF;
    INSERT INTO "Roles" ("Name") SELECT 'SuperAdmin'
    WHERE NOT EXISTS (SELECT 1 FROM "Roles" WHERE "Name" = 'SuperAdmin');
    SELECT "Id" INTO STRICT admin_role FROM "Roles" WHERE "Name" = 'SuperAdmin';
    INSERT INTO "UserRoles" ("UserId", "RoleId") VALUES (target_id, admin_role) ON CONFLICT DO NOTHING;
    UPDATE "RefreshTokens" SET "RevokedAt" = now() WHERE "UserId" = target_id AND "RevokedAt" IS NULL;
END $$;
COMMIT;
-- Log in again to receive a token with SuperAdmin. Role removal takes effect
-- immediately for branding endpoints because the service checks the database.
