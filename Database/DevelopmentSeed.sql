-- Development fixtures from the mobile catalog. Schedule the next camp on a
-- Sunday so freshly installed data still has upcoming, live and past events.
-- Stable fixture IDs allow existing bookings to keep their purchased version.
DO $seed$
DECLARE
    ada CONSTANT uuid := '6b6b0000-0000-4000-8000-000000000001';
    deniz CONSTANT uuid := '6b6b0000-0000-4000-8000-000000000002';
    coach CONSTANT uuid := '6b6b0000-0000-4000-8000-000000000003';
    hiking CONSTANT uuid := '6b6b0000-0000-4000-8000-000000000004';
    forest CONSTANT uuid := '6b6b0000-0000-4000-8000-000000000005';
    zone CONSTANT text := 'Europe/Istanbul';
    today date := (CURRENT_TIMESTAMP AT TIME ZONE 'Europe/Istanbul')::date;
    next_sunday date := today + (7 - extract(isodow FROM today)::integer % 7);
    e record;
    event_date date;
    event_start timestamptz;
    event_end timestamptz;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended('vubes-development-seed', 0));
    IF EXISTS (SELECT 1 FROM event_version WHERE id = md5('vubes:version:camp')::uuid) THEN
        RETURN;
    END IF;

    CREATE TEMP TABLE demo_people (
        person_key text PRIMARY KEY, member_id uuid NOT NULL, phone text NOT NULL,
        display_name text NOT NULL, portrait text NOT NULL, cover text
    ) ON COMMIT DROP;

    INSERT INTO demo_people VALUES
        ('ada', ada, '+905555555555', 'Ada Kaya', 'Portrait3', 'Terrace'),
        ('deniz', deniz, '+905550000090', 'Deniz Kaya', 'Portrait0', 'Likya'),
        ('ece', md5('vubes:member:ece')::uuid, '+905550000091', 'Ece Arslan', 'Portrait1', 'Terrace'),
        ('mert', md5('vubes:member:mert')::uuid, '+905550000092', 'Mert Demir', 'Portrait2', 'Forest'),
        ('can', md5('vubes:member:can')::uuid, '+905550000093', 'Can', 'Portrait4', NULL),
        ('selin', md5('vubes:member:selin')::uuid, '+905550000094', 'Selin', 'Portrait5', NULL);

    INSERT INTO member (id, phone, full_name, status, visibility, avatar, cover)
    SELECT member_id, phone, display_name, 'active', CASE WHEN person_key = 'ada' THEN 'private' ELSE 'club' END,
           CASE portrait WHEN 'Portrait3' THEN '/media/Portrait3.png' WHEN 'Portrait2' THEN '/media/Portrait2.png' WHEN 'Coast' THEN '/media/Coast.jpg' WHEN 'DiscoverTerrain' THEN '/media/DiscoverTerrain.png' WHEN 'Forest' THEN '/media/Forest.png' WHEN 'Portrait4' THEN '/media/Portrait4.png' WHEN 'Portrait5' THEN '/media/Portrait5.png' WHEN 'Dusk' THEN '/media/Dusk.jpg' WHEN 'Terrace' THEN '/media/Terrace.png' WHEN 'Portrait0' THEN '/media/Portrait0.png' WHEN 'Portrait1' THEN '/media/Portrait1.png' WHEN 'Cycling' THEN '/media/Cycling.jpg' WHEN 'Wordmark' THEN '/media/Wordmark.png' WHEN 'Likya' THEN '/media/Likya.jpg' END, CASE cover WHEN 'Portrait3' THEN '/media/Portrait3.png' WHEN 'Portrait2' THEN '/media/Portrait2.png' WHEN 'Coast' THEN '/media/Coast.jpg' WHEN 'DiscoverTerrain' THEN '/media/DiscoverTerrain.png' WHEN 'Forest' THEN '/media/Forest.png' WHEN 'Portrait4' THEN '/media/Portrait4.png' WHEN 'Portrait5' THEN '/media/Portrait5.png' WHEN 'Dusk' THEN '/media/Dusk.jpg' WHEN 'Terrace' THEN '/media/Terrace.png' WHEN 'Portrait0' THEN '/media/Portrait0.png' WHEN 'Portrait1' THEN '/media/Portrait1.png' WHEN 'Cycling' THEN '/media/Cycling.jpg' WHEN 'Wordmark' THEN '/media/Wordmark.png' WHEN 'Likya' THEN '/media/Likya.jpg' END
    FROM demo_people
    ON CONFLICT (id) DO UPDATE SET
        avatar = COALESCE(member.avatar, EXCLUDED.avatar),
        cover = COALESCE(member.cover, EXCLUDED.cover);

    CREATE TEMP TABLE demo_trainers (
        person_key text PRIMARY KEY, trainer_id uuid NOT NULL, specialty text, experience text,
        location text, bio text, crew_name text
    ) ON COMMIT DROP;

    INSERT INTO demo_trainers VALUES
        ('deniz', coach, 'Trail running & endurance', '8 years coaching', 'Kaş & Istanbul',
         'Deniz brings runners together on coastal and forest trails. His sessions pair steady pacing with practical trail technique and time to recover.', 'Coastal Crew'),
        ('ece', md5('vubes:trainer:ece')::uuid, 'Mobility & recovery', '6 years coaching', 'Istanbul',
         'Ece coaches small groups through mobility, breathwork and recovery. Her sessions welcome members looking to move more comfortably at their own pace.', 'Sunday Studio'),
        ('mert', md5('vubes:trainer:mert')::uuid, 'Hiking & outdoor conditioning', '7 years coaching', 'Istanbul',
         'Mert leads forest walks and ridge hikes. He shares route preparation, outdoor skills and conditioning tips throughout the day.', 'Forest Circle');

    INSERT INTO trainer (id, member_id, specialty, bio, experience, location, cover)
    SELECT t.trainer_id, p.member_id, t.specialty, t.bio, t.experience, t.location, CASE p.cover WHEN 'Portrait3' THEN '/media/Portrait3.png' WHEN 'Portrait2' THEN '/media/Portrait2.png' WHEN 'Coast' THEN '/media/Coast.jpg' WHEN 'DiscoverTerrain' THEN '/media/DiscoverTerrain.png' WHEN 'Forest' THEN '/media/Forest.png' WHEN 'Portrait4' THEN '/media/Portrait4.png' WHEN 'Portrait5' THEN '/media/Portrait5.png' WHEN 'Dusk' THEN '/media/Dusk.jpg' WHEN 'Terrace' THEN '/media/Terrace.png' WHEN 'Portrait0' THEN '/media/Portrait0.png' WHEN 'Portrait1' THEN '/media/Portrait1.png' WHEN 'Cycling' THEN '/media/Cycling.jpg' WHEN 'Wordmark' THEN '/media/Wordmark.png' WHEN 'Likya' THEN '/media/Likya.jpg' END
    FROM demo_trainers t JOIN demo_people p USING (person_key)
    ON CONFLICT (id) DO UPDATE SET specialty = EXCLUDED.specialty, bio = EXCLUDED.bio,
        experience = EXCLUDED.experience, location = EXCLUDED.location, cover = EXCLUDED.cover;

    INSERT INTO crew_row (id, trainer_id, name)
    SELECT md5('vubes:crew:' || person_key)::uuid, trainer_id, crew_name FROM demo_trainers;

    INSERT INTO crew_mate (id, crew_id, member_id)
    SELECT gen_random_uuid(), md5('vubes:crew:' || c.crew)::uuid, p.member_id
    FROM (VALUES ('deniz','ada'), ('deniz','mert'), ('deniz','can'), ('ece','selin'),
                 ('ece','ada'), ('mert','can'), ('mert','selin')) AS c(crew, person_key)
    JOIN demo_people p USING (person_key);

    INSERT INTO invitation (id, digest, trainer_id, expires)
    VALUES (gen_random_uuid(), upper(encode(sha256(convert_to('AFTERGLOW', 'UTF8')), 'hex')),
            coach, CURRENT_TIMESTAMP + interval '30 days')
    ON CONFLICT (digest) DO NOTHING;

    INSERT INTO sport_row (id, code, label, fallback_cover)
    SELECT CASE WHEN s.code = 'hiking' THEN hiking ELSE md5('vubes:sport:' || s.code)::uuid END,
           s.code, s.label, CASE s.cover WHEN 'Portrait3' THEN '/media/Portrait3.png' WHEN 'Portrait2' THEN '/media/Portrait2.png' WHEN 'Coast' THEN '/media/Coast.jpg' WHEN 'DiscoverTerrain' THEN '/media/DiscoverTerrain.png' WHEN 'Forest' THEN '/media/Forest.png' WHEN 'Portrait4' THEN '/media/Portrait4.png' WHEN 'Portrait5' THEN '/media/Portrait5.png' WHEN 'Dusk' THEN '/media/Dusk.jpg' WHEN 'Terrace' THEN '/media/Terrace.png' WHEN 'Portrait0' THEN '/media/Portrait0.png' WHEN 'Portrait1' THEN '/media/Portrait1.png' WHEN 'Cycling' THEN '/media/Cycling.jpg' WHEN 'Wordmark' THEN '/media/Wordmark.png' WHEN 'Likya' THEN '/media/Likya.jpg' END
    FROM (VALUES ('trailRunning', 'Trail running', 'Likya'), ('running', 'Running', 'Forest'),
                 ('mobility', 'Mobility', 'Terrace'), ('hiking', 'Hiking', 'Forest'),
                 ('walking', 'Walking', 'Dusk'), ('cycling', 'Cycling', 'Cycling')) AS s(code, label, cover)
    ON CONFLICT (id) DO UPDATE SET label = EXCLUDED.label, fallback_cover = EXCLUDED.fallback_cover;

    CREATE TEMP TABLE demo_events (
        event_key text PRIMARY KEY, title text, place text, coach_key text, sport text, difficulty text,
        cover text, price_lira integer, distance_meters integer, offset_days integer, first_time time,
        last_day integer, last_time time, latitude double precision, longitude double precision,
        meeting text, preparation text, lodging text
    ) ON COMMIT DROP;

    INSERT INTO demo_events VALUES
        ('camp','Likya Trail & Mobility Camp','Kaş','deniz','trailRunning','moderate','Likya',4850,NULL,
         0,'08:30',2,'17:30',NULL,NULL,'Kaş Marina',NULL,'Two nights at the club’s lodge are included in the full program.'),
        ('forest','Belgrad Forest Run','Istanbul','deniz','running','easy','Forest',950,6000,
         7,'09:00',0,'10:15',41.1764,28.9820,'Belgrad Forest trailhead',NULL,NULL),
        ('mobility','Sunday Mobility','Istanbul','ece','mobility','easy','Terrace',750,NULL,
         14,'10:00',0,'11:00',41.0621,29.0098,'Club terrace','Bring a mat, water and comfortable layers.',NULL),
        ('hike','Bosphorus Ridge Hike','Istanbul','mert','hiking','moderate','Forest',1250,8000,
         21,'09:00',0,'11:00',41.1353,29.0938,'Ridge trailhead',NULL,NULL),
        ('cycling','Bosphorus Sunrise Ride','Istanbul','deniz','cycling','moderate','Cycling',1150,18000,
         22,'08:00',0,'10:15',41.0770,29.0438,'Bebek waterfront','Bring a roadworthy bicycle, helmet, lights and water.',NULL),
        ('walk','Marmara Waterfront Walk','Istanbul','mert','walking','easy','Dusk',550,4000,
         28,'09:30',0,'10:45',40.9680,28.8004,'Florya promenade','Bring comfortable walking shoes, water and a light layer.',NULL),
        ('live','Likya Coastal Run','Kaş','deniz','trailRunning','moderate','Likya',1250,6000,
         NULL,'00:00',1,'00:00',36.2024,29.6388,'Kaş Marina',NULL,NULL),
        ('past','Forest Walk','Istanbul','mert','hiking','easy','Forest',750,NULL,
         -21,'09:00',0,'10:00',41.1764,28.9820,'Belgrad Forest trailhead',NULL,NULL),
        ('recovery','Terrace Recovery','Istanbul','ece','mobility','easy','Terrace',750,NULL,
         -27,'10:00',0,'12:00',41.0621,29.0098,'Club terrace',NULL,NULL),
        ('coastal','Coastal Trail Morning','Kaş','deniz','trailRunning','moderate','Likya',1250,NULL,
         -34,'08:30',0,'11:00',36.2024,29.6388,'Kaş Marina',NULL,NULL);

    FOR e IN
        SELECT d.*, t.trainer_id, s.id AS sport_id,
               CASE WHEN d.event_key = 'forest' THEN forest ELSE md5('vubes:event:' || d.event_key)::uuid END AS event_id,
               md5('vubes:version:' || d.event_key)::uuid AS version_id
        FROM demo_events d JOIN demo_trainers t ON t.person_key = d.coach_key
        JOIN sport_row s ON s.code = d.sport
    LOOP
        event_date := COALESCE(next_sunday + e.offset_days, today);
        event_start := (event_date + e.first_time) AT TIME ZONE zone;
        event_end := ((event_date + e.last_day) + e.last_time) AT TIME ZONE zone;

        INSERT INTO club_event (id, trainer_id, published, cancelled, capacity, minimum_confirmed, opens, closes)
        VALUES (e.event_id, e.trainer_id, true, false, 36, 3,
                LEAST(CURRENT_TIMESTAMP, event_start) - interval '30 days', event_start - interval '30 minutes')
        ON CONFLICT (id) DO UPDATE SET opens = EXCLUDED.opens, closes = EXCLUDED.closes;

        INSERT INTO event_version (id, event_id, revision, title, place, starts, ends, time_zone,
            sport_id, level, cover, distance_meters, amount, currency, latitude, longitude, meeting,
            preparation, lodging, terms_version, terms_url)
        VALUES (e.version_id, e.event_id, 'mobile-v1', e.title, e.place, event_start, event_end, zone,
            e.sport_id, e.difficulty, CASE e.cover WHEN 'Portrait3' THEN '/media/Portrait3.png' WHEN 'Portrait2' THEN '/media/Portrait2.png' WHEN 'Coast' THEN '/media/Coast.jpg' WHEN 'DiscoverTerrain' THEN '/media/DiscoverTerrain.png' WHEN 'Forest' THEN '/media/Forest.png' WHEN 'Portrait4' THEN '/media/Portrait4.png' WHEN 'Portrait5' THEN '/media/Portrait5.png' WHEN 'Dusk' THEN '/media/Dusk.jpg' WHEN 'Terrace' THEN '/media/Terrace.png' WHEN 'Portrait0' THEN '/media/Portrait0.png' WHEN 'Portrait1' THEN '/media/Portrait1.png' WHEN 'Cycling' THEN '/media/Cycling.jpg' WHEN 'Wordmark' THEN '/media/Wordmark.png' WHEN 'Likya' THEN '/media/Likya.jpg' END, e.distance_meters, e.price_lira * 100, 'TRY',
            e.latitude, e.longitude, e.meeting, COALESCE(e.preparation, 'Bring trail shoes, water, a light layer and sun protection.'),
            e.lodging, 'demo-v1', '/development/event-terms/v1');

        UPDATE club_event SET current_version_id = e.version_id WHERE id = e.event_id;
    END LOOP;

    INSERT INTO additional_sport (id, version_id, sport_id)
    SELECT gen_random_uuid(), md5('vubes:version:camp')::uuid, id FROM sport_row WHERE code = 'mobility';

    CREATE TEMP TABLE demo_sessions (
        event_key text NOT NULL, day_index integer NOT NULL, ordinal integer NOT NULL,
        local_start time NOT NULL, minutes integer NOT NULL, title text NOT NULL, details text NOT NULL,
        PRIMARY KEY (event_key, day_index, ordinal)
    ) ON COMMIT DROP;

    INSERT INTO demo_sessions VALUES
        ('camp', 0, 1, '08:30', 30, 'Meet at Kaş Marina', 'Check-in · 30 min'),
        ('camp', 0, 2, '09:00', 90, 'Coastal trail run', 'Guided group run · 1 hr 30 min'),
        ('camp', 0, 3, '10:30', 45, 'Mobility session', 'Stretch & recover · 45 min'),
        ('camp', 0, 4, '11:30', 60, 'Lunch on the terrace', 'Group lunch · 1 hr'),
        ('camp', 0, 5, '14:00', 45, 'Trail technique clinic', 'Footwork and pacing · 45 min'),
        ('camp', 0, 6, '16:00', 45, 'Coastal cool-down', 'Easy walk & guided stretch · 45 min'),
        ('camp', 0, 7, '17:30', 30, 'Sunset debrief', 'Terrace tea & tomorrow’s route · 30 min'),
        ('camp', 1, 1, '08:00', 45, 'Breakfast', 'At the lodge · 45 min'),
        ('camp', 1, 2, '09:00', 120, 'Lycian Way trail', 'Guided run · 2 hr'),
        ('camp', 1, 3, '11:30', 60, 'Recovery & lunch', 'Mobility and a shared meal · 1 hr'),
        ('camp', 1, 4, '14:30', 45, 'Route-reading workshop', 'Navigation with Deniz · 45 min'),
        ('camp', 1, 5, '16:30', 45, 'Mobility reset', 'Guided recovery · 45 min'),
        ('camp', 1, 6, '17:30', 30, 'Crew wind-down', 'Tea and trail stories · 30 min'),
        ('camp', 2, 1, '08:30', 45, 'Sunrise mobility', 'Terrace session · 45 min'),
        ('camp', 2, 2, '10:00', 60, 'Coastal walk', 'Easy group walk · 1 hr'),
        ('camp', 2, 3, '11:30', 30, 'Lodge check-out', 'Pack & store bags · 30 min'),
        ('camp', 2, 4, '13:00', 60, 'Lunch in Kaş', 'Shared table · 1 hr'),
        ('camp', 2, 5, '15:30', 45, 'Your next trail', 'Personal training takeaways · 45 min'),
        ('camp', 2, 6, '17:00', 30, 'Club send-off', 'Meet at Kaş Marina · 30 min'),
        ('forest', 0, 1, '09:00', 15, 'Meet at the trailhead', 'Warm-up · 15 min'),
        ('forest', 0, 2, '09:15', 60, 'Forest loop', 'Guided group run · 1 hr'),
        ('mobility', 0, 1, '10:00', 60, 'Terrace mobility', 'Guided session · 1 hr'),
        ('hike', 0, 1, '09:00', 120, 'Ridge hike', 'Guided hike · 2 hr'),
        ('cycling', 0, 1, '08:00', 15, 'Meet by the waterfront', 'Bike check · 15 min'),
        ('cycling', 0, 2, '08:15', 90, 'Bosphorus ride', 'Guided group ride · 1 hr 30 min'),
        ('cycling', 0, 3, '09:45', 30, 'Coffee by the water', 'Group cool-down · 30 min'),
        ('walk', 0, 1, '09:30', 15, 'Meet on the promenade', 'Group introduction · 15 min'),
        ('walk', 0, 2, '09:45', 60, 'Walk along the coast', 'Easy group walk · 1 hr'),
        ('live', 0, 1, '09:00', 90, 'Coastal trail run', 'Guided group run · 1 hr 30 min'),
        ('live', 0, 2, '10:30', 45, 'Recovery', 'Stretch & recover · 45 min'),
        ('past', 0, 1, '09:00', 60, 'Forest walk', 'Group hike · 1 hr'),
        ('recovery', 0, 1, '10:00', 60, 'Mobility practice', 'Guided movement · 1 hr'),
        ('recovery', 0, 2, '11:15', 45, 'Breathwork & tea', 'Terrace session · 45 min'),
        ('coastal', 0, 1, '08:30', 90, 'Coastal loop', 'Guided trail run · 90 min'),
        ('coastal', 0, 2, '10:30', 30, 'Recovery & stretch', 'Cool-down · 30 min');

    INSERT INTO program_day_row (id, version_id, day, ordinal)
    SELECT gen_random_uuid(), v.id, (v.starts AT TIME ZONE zone)::date + days.day_index, days.day_index + 1
    FROM (SELECT DISTINCT event_key, day_index FROM demo_sessions) days
    JOIN event_version v ON v.id = md5('vubes:version:' || days.event_key)::uuid;

    INSERT INTO program_slot (id, day_id, ordinal, starts, ends, title, details)
    SELECT gen_random_uuid(), d.id, s.ordinal, (d.day + s.local_start) AT TIME ZONE zone,
           ((d.day + s.local_start) AT TIME ZONE zone) + make_interval(mins => s.minutes), s.title, s.details
    FROM demo_sessions s JOIN program_day_row d
      ON d.version_id = md5('vubes:version:' || s.event_key)::uuid AND d.ordinal = s.day_index + 1;

    -- Reconstruct the published counts from individual attended bookings. The
    -- fixture members are synthetic, private and have no configured login.
    INSERT INTO member (id, phone, full_name, status, visibility)
    SELECT md5('vubes:reviewer:' || n)::uuid, '+905550001' || lpad(n::text, 3, '0'),
           'Club member ' || n, 'active', 'private'
    FROM generate_series(1, 24) AS n;

    CREATE TEMP TABLE demo_bookings (
        member_id uuid NOT NULL, event_key text NOT NULL, attendance text NOT NULL, stars integer,
        hold_id uuid NOT NULL DEFAULT gen_random_uuid(), payment_id uuid NOT NULL DEFAULT gen_random_uuid(),
        booking_id uuid NOT NULL DEFAULT gen_random_uuid(), PRIMARY KEY(member_id, event_key)
    ) ON COMMIT DROP;

    INSERT INTO demo_bookings (member_id, event_key, attendance, stars)
    SELECT md5('vubes:reviewer:' || n)::uuid, rated.event_key, 'attended',
           CASE WHEN n <= rated.number_of_fours THEN 4 ELSE 5 END
    FROM (VALUES ('past', 20, 6), ('recovery', 16, 2), ('coastal', 24, 6))
        AS rated(event_key, reviewers, number_of_fours)
    CROSS JOIN LATERAL generate_series(1, rated.reviewers) AS n;

    INSERT INTO demo_bookings (member_id, event_key, attendance) VALUES (ada, 'past', 'attended');

    INSERT INTO demo_bookings (member_id, event_key, attendance)
    SELECT member_id, 'live', 'checkedIn' FROM demo_people;

    INSERT INTO hold_row (id, member_id, event_id, version_id, state, expires, amount, currency, terms_version, accepted_at)
    SELECT b.hold_id, b.member_id, v.event_id, v.id, 'confirmed', v.starts - interval '1 day' + interval '10 minutes',
           v.amount, v.currency, v.terms_version, v.starts - interval '1 day'
    FROM demo_bookings b JOIN event_version v ON v.id = md5('vubes:version:' || b.event_key)::uuid;

    INSERT INTO payment_row (id, hold_id, provider, reference, amount, currency, status, paid_at)
    SELECT b.payment_id, h.id, 'development-seed', 'fixture-' || b.payment_id, h.amount, h.currency,
           'succeeded', h.accepted_at + interval '1 minute'
    FROM demo_bookings b JOIN hold_row h ON h.id = b.hold_id;

    INSERT INTO booking_row (id, member_id, event_id, version_id, payment_id, status, attendance)
    SELECT b.booking_id, b.member_id, h.event_id, h.version_id, b.payment_id, 'confirmed', b.attendance
    FROM demo_bookings b JOIN hold_row h ON h.id = b.hold_id;

    INSERT INTO pass_row (id, booking_id, nonce, revoked)
    SELECT gen_random_uuid(), booking_id, gen_random_uuid()::text, false FROM demo_bookings;

    INSERT INTO rating_row (id, member_id, event_id, stars, moderation)
    SELECT gen_random_uuid(), b.member_id, h.event_id, b.stars, 'published'
    FROM demo_bookings b JOIN hold_row h ON h.id = b.hold_id WHERE b.stars IS NOT NULL;

    -- Representative Kaş coastal coordinates for the prototype's illustrated
    -- route. These are fictional observations, not measured device histories.
    INSERT INTO route_point (id, event_id, ordinal, latitude, longitude)
    SELECT gen_random_uuid(), md5('vubes:event:live')::uuid, r.ordinal, r.lat, r.lon
    FROM (VALUES
        (1,36.2024,29.6388), (2,36.2015,29.6395), (3,36.1999,29.6404),
        (4,36.1990,29.6422), (5,36.1982,29.6438), (6,36.1968,29.6445),
        (7,36.1962,29.6436), (8,36.1954,29.6428), (9,36.1947,29.6415),
        (10,36.1938,29.6424), (11,36.1933,29.6450), (12,36.1928,29.6465),
        (13,36.1921,29.6480), (14,36.1916,29.6505), (15,36.1910,29.6512)
    ) AS r(ordinal, lat, lon);

    -- Only fixture peers share locations. The signed-in member must opt in
    -- before the API will record their device's first measurement.
    INSERT INTO location_share (id, event_id, member_id, alias, expires, revoked)
    SELECT gen_random_uuid(), v.event_id, p.member_id, gen_random_uuid(), v.ends, false
    FROM demo_people p JOIN event_version v ON v.id = md5('vubes:version:live')::uuid
    WHERE p.person_key <> 'ada';

    INSERT INTO last_location (id, share_id, sequence, recorded, latitude, longitude)
    SELECT gen_random_uuid(), s.id, 1, CURRENT_TIMESTAMP, r.latitude, r.longitude
    FROM (VALUES ('deniz',2), ('ece',4), ('mert',6), ('can',11), ('selin',13)) AS sample(person_key, ordinal)
    JOIN demo_people p USING (person_key)
    JOIN location_share s ON s.member_id = p.member_id AND s.event_id = md5('vubes:event:live')::uuid
    JOIN route_point r ON r.event_id = s.event_id AND r.ordinal = sample.ordinal;
END;
$seed$;
