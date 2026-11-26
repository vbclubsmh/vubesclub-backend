using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VubesClub.Api.Infrastructure;

[DbContext(typeof(ClubDb))]
[Migration("202611270001_MemberProfile")]
public sealed class MemberProfile : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        ALTER TABLE member
            ADD COLUMN IF NOT EXISTS height_cm integer,
            ADD COLUMN IF NOT EXISTS weight_kg integer,
            ADD COLUMN IF NOT EXISTS clothing_size text,
            ADD COLUMN IF NOT EXISTS birth_date date;
        UPDATE member m SET height_cm = a.height_cm, weight_kg = a.weight_kg
            FROM application_entry a WHERE a.member_id = m.id AND m.height_cm IS NULL AND m.weight_kg IS NULL;
        ALTER TABLE otp_challenge ADD COLUMN IF NOT EXISTS birth_date date;
        ALTER TABLE application_entry ADD COLUMN IF NOT EXISTS birth_date date;

        CREATE TABLE contact_change (
            id uuid PRIMARY KEY,
            member_id uuid NOT NULL,
            new_email text,
            new_phone text,
            digest text NOT NULL,
            expires timestamptz NOT NULL,
            resend_at timestamptz NOT NULL,
            created timestamptz NOT NULL,
            attempts integer NOT NULL,
            consumed boolean NOT NULL
        );
        CREATE INDEX contact_change_member ON contact_change (member_id, created);
        """);

    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Restore a database backup to reverse this migration.");
}
