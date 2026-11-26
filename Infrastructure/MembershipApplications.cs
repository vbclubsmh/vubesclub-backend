using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611260001_MembershipApplications")]
public sealed class MembershipApplications : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        ALTER TABLE member ADD COLUMN IF NOT EXISTS email text;
        CREATE UNIQUE INDEX IF NOT EXISTS member_u2 ON member (email) WHERE email IS NOT NULL;

        ALTER TABLE otp_challenge
            ADD COLUMN IF NOT EXISTS email text,
            ADD COLUMN IF NOT EXISTS university text,
            ADD COLUMN IF NOT EXISTS job_title text,
            ADD COLUMN IF NOT EXISTS workplace text,
            ADD COLUMN IF NOT EXISTS instagram text,
            ADD COLUMN IF NOT EXISTS linked_in text;

        ALTER TABLE application_entry ALTER COLUMN invitation_id DROP NOT NULL;
        ALTER TABLE application_entry
            ADD COLUMN IF NOT EXISTS university text NOT NULL DEFAULT 'Not provided',
            ADD COLUMN IF NOT EXISTS job_title text NOT NULL DEFAULT 'Not provided',
            ADD COLUMN IF NOT EXISTS workplace text,
            ADD COLUMN IF NOT EXISTS instagram text,
            ADD COLUMN IF NOT EXISTS linked_in text,
            ADD COLUMN IF NOT EXISTS submitted_at timestamptz NOT NULL DEFAULT now();
        ALTER TABLE application_entry ALTER COLUMN university DROP DEFAULT;
        ALTER TABLE application_entry ALTER COLUMN job_title DROP DEFAULT;
        ALTER TABLE application_entry ALTER COLUMN submitted_at DROP DEFAULT;
        DROP INDEX IF EXISTS application_entry_u1;
        CREATE UNIQUE INDEX IF NOT EXISTS application_entry_u1 ON application_entry (invitation_id)
            WHERE invitation_id IS NOT NULL;

        """);

    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Restore a database backup to reverse this migration.");
}
