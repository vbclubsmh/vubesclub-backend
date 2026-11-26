using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611260003_LinkedInColumn")]
public sealed class LinkedInColumn : Migration
{
    // Databases migrated before the column name followed the snake_case mapping used by every entity.
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        DO $$
        DECLARE t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY['otp_challenge', 'application_entry'] LOOP
                IF EXISTS (SELECT FROM information_schema.columns
                           WHERE table_schema = current_schema() AND table_name = t AND column_name = 'linkedin') THEN
                    EXECUTE format('ALTER TABLE %I RENAME COLUMN linkedin TO linked_in', t);
                END IF;
            END LOOP;
        END $$;
        """);

    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Restore a database backup to reverse this migration.");
}
