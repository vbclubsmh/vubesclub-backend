-- Rebase the imported catalogue for a local demonstration. Only stable fixture
-- version IDs are moved, and only when the sample live event is on another day.
DO $refresh$
DECLARE
    shift interval;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended('vubes-development-seed', 0));
    SELECT ((CURRENT_TIMESTAMP AT TIME ZONE 'Europe/Istanbul')::date -
            (starts AT TIME ZONE 'Europe/Istanbul')::date) * interval '1 day'
    INTO shift FROM event_version WHERE id = md5('vubes:version:live')::uuid;
    IF shift IS NULL OR shift = interval '0' THEN RETURN; END IF;

    CREATE TEMP TABLE fixture_versions ON COMMIT DROP AS
    SELECT id, event_id FROM event_version
    WHERE id IN (SELECT md5('vubes:version:' || k)::uuid
                 FROM unnest(ARRAY['camp','forest','mobility','hike','cycling','walk','live','past','recovery','coastal']) k);

    UPDATE event_version SET starts = starts + shift, ends = ends + shift
    WHERE id IN (SELECT id FROM fixture_versions);
    UPDATE club_event SET opens = opens + shift, closes = closes + shift,
        premium_opens = premium_opens + shift WHERE id IN (SELECT event_id FROM fixture_versions);
    UPDATE program_slot SET starts = starts + shift, ends = ends + shift
    WHERE day_id IN (SELECT id FROM program_day_row WHERE version_id IN (SELECT id FROM fixture_versions));
    UPDATE program_day_row SET day = day + shift
    WHERE version_id IN (SELECT id FROM fixture_versions);
    UPDATE location_share SET expires = expires + shift
    WHERE event_id = md5('vubes:event:live')::uuid AND NOT revoked;
END;
$refresh$;
