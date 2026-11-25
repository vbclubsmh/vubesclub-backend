CREATE TABLE member (
    id uuid PRIMARY KEY,
    phone text NOT NULL,
    full_name text NOT NULL,
    status text NOT NULL,
    avatar text,
    cover text,
    visibility text NOT NULL
);

CREATE TABLE trainer (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    specialty text NOT NULL,
    bio text NOT NULL,
    experience text NOT NULL,
    location text NOT NULL,
    cover text
);

CREATE TABLE invitation (
    id uuid PRIMARY KEY,
    digest text NOT NULL,
    trainer_id uuid NOT NULL,
    expires timestamptz NOT NULL,
    used_by uuid
);

CREATE TABLE application_entry (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    invitation_id uuid NOT NULL,
    adult_declared_at timestamptz NOT NULL
);

CREATE TABLE otp_challenge (
    id uuid PRIMARY KEY,
    phone text NOT NULL,
    invite_id uuid,
    full_name text,
    digest text NOT NULL,
    expires timestamptz NOT NULL,
    resend_at timestamptz NOT NULL,
    created timestamptz NOT NULL,
    attempts integer NOT NULL,
    consumed boolean NOT NULL
);

CREATE TABLE mobile_session (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    access_digest text NOT NULL,
    refresh_digest text NOT NULL,
    access_expires timestamptz NOT NULL,
    expires timestamptz NOT NULL,
    revoked boolean NOT NULL
);

CREATE TABLE subscription (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    starts timestamptz NOT NULL,
    ends timestamptz NOT NULL
);

CREATE TABLE sport_row (
    id uuid PRIMARY KEY,
    code text NOT NULL,
    label text NOT NULL,
    fallback_cover text
);

CREATE TABLE club_event (
    id uuid PRIMARY KEY,
    trainer_id uuid NOT NULL,
    current_version_id uuid,
    published boolean NOT NULL,
    cancelled boolean NOT NULL,
    capacity integer NOT NULL,
    minimum_confirmed integer NOT NULL,
    opens timestamptz NOT NULL,
    closes timestamptz NOT NULL,
    premium_opens timestamptz
);

CREATE TABLE event_version (
    id uuid PRIMARY KEY,
    event_id uuid NOT NULL,
    revision text NOT NULL,
    title text NOT NULL,
    place text NOT NULL,
    starts timestamptz NOT NULL,
    ends timestamptz NOT NULL,
    time_zone text NOT NULL,
    sport_id uuid NOT NULL,
    level text NOT NULL,
    cover text,
    distance_meters integer,
    amount bigint NOT NULL,
    currency text NOT NULL,
    latitude double precision,
    longitude double precision,
    meeting text NOT NULL,
    directions text,
    preparation text,
    lodging text,
    transport text,
    terms_version text NOT NULL,
    terms_url text NOT NULL
);

CREATE TABLE additional_sport (
    id uuid PRIMARY KEY,
    version_id uuid NOT NULL,
    sport_id uuid NOT NULL
);

CREATE TABLE program_day_row (
    id uuid PRIMARY KEY,
    version_id uuid NOT NULL,
    day date NOT NULL,
    ordinal integer NOT NULL
);

CREATE TABLE program_slot (
    id uuid PRIMARY KEY,
    day_id uuid NOT NULL,
    ordinal integer NOT NULL,
    starts timestamptz NOT NULL,
    ends timestamptz,
    title text NOT NULL,
    details text NOT NULL
);

CREATE TABLE saved_event (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    event_id uuid NOT NULL
);

CREATE TABLE follow (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    trainer_id uuid NOT NULL
);

CREATE TABLE introduction_row (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    trainer_id uuid NOT NULL,
    state text NOT NULL,
    requested_at timestamptz NOT NULL
);

CREATE TABLE trainer_link (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    trainer_id uuid NOT NULL
);

CREATE TABLE crew_row (
    id uuid PRIMARY KEY,
    trainer_id uuid NOT NULL,
    name text NOT NULL
);

CREATE TABLE crew_mate (
    id uuid PRIMARY KEY,
    crew_id uuid NOT NULL,
    member_id uuid NOT NULL
);

CREATE TABLE hold_row (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    event_id uuid NOT NULL,
    version_id uuid NOT NULL,
    state text NOT NULL,
    expires timestamptz NOT NULL,
    verify_until timestamptz,
    amount bigint NOT NULL,
    currency text NOT NULL,
    terms_version text NOT NULL,
    accepted_at timestamptz
);

CREATE TABLE payment_row (
    id uuid PRIMARY KEY,
    hold_id uuid NOT NULL,
    provider text NOT NULL,
    reference text,
    amount bigint NOT NULL,
    currency text NOT NULL,
    status text NOT NULL,
    redirect text,
    paid_at timestamptz
);

CREATE TABLE payment_notice (
    id uuid PRIMARY KEY,
    provider text NOT NULL,
    external_id text NOT NULL,
    payment_id uuid NOT NULL,
    received timestamptz NOT NULL
);

CREATE TABLE refund_row (
    id uuid PRIMARY KEY,
    payment_id uuid NOT NULL,
    reason text NOT NULL,
    status text NOT NULL
);

CREATE TABLE request_key (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    operation text NOT NULL,
    key text NOT NULL,
    fingerprint text NOT NULL,
    resource_id uuid NOT NULL
);

CREATE TABLE booking_row (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    event_id uuid NOT NULL,
    version_id uuid NOT NULL,
    payment_id uuid NOT NULL,
    status text NOT NULL,
    attendance text NOT NULL
);

CREATE TABLE pass_row (
    id uuid PRIMARY KEY,
    booking_id uuid NOT NULL,
    nonce text NOT NULL,
    revoked boolean NOT NULL
);

CREATE TABLE assignment (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    event_id uuid NOT NULL,
    starts timestamptz NOT NULL,
    ends timestamptz NOT NULL
);

CREATE TABLE rating_row (
    id uuid PRIMARY KEY,
    member_id uuid NOT NULL,
    event_id uuid NOT NULL,
    stars integer NOT NULL,
    moderation text NOT NULL
);

CREATE TABLE route_point (
    id uuid PRIMARY KEY,
    event_id uuid NOT NULL,
    ordinal integer NOT NULL,
    latitude double precision NOT NULL,
    longitude double precision NOT NULL
);

CREATE TABLE location_share (
    id uuid PRIMARY KEY,
    event_id uuid NOT NULL,
    member_id uuid NOT NULL,
    alias uuid NOT NULL,
    expires timestamptz NOT NULL,
    revoked boolean NOT NULL
);

CREATE TABLE last_location (
    id uuid PRIMARY KEY,
    share_id uuid NOT NULL,
    sequence integer NOT NULL,
    recorded timestamptz NOT NULL,
    latitude double precision NOT NULL,
    longitude double precision NOT NULL
);

CREATE UNIQUE INDEX member_u1 ON member (phone);

ALTER TABLE trainer ADD FOREIGN KEY (member_id) REFERENCES member(id);
CREATE UNIQUE INDEX trainer_u1 ON trainer (member_id);

ALTER TABLE invitation ADD FOREIGN KEY (trainer_id) REFERENCES trainer(id);
ALTER TABLE invitation ADD FOREIGN KEY (used_by) REFERENCES member(id);
CREATE UNIQUE INDEX invitation_u1 ON invitation (digest);

ALTER TABLE application_entry ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE application_entry ADD FOREIGN KEY (invitation_id) REFERENCES invitation(id);
CREATE UNIQUE INDEX application_entry_u1 ON application_entry (invitation_id);

ALTER TABLE otp_challenge ADD FOREIGN KEY (invite_id) REFERENCES invitation(id);

ALTER TABLE mobile_session ADD FOREIGN KEY (member_id) REFERENCES member(id);
CREATE UNIQUE INDEX mobile_session_u1 ON mobile_session (access_digest);
CREATE UNIQUE INDEX mobile_session_u2 ON mobile_session (refresh_digest);
CREATE UNIQUE INDEX mobile_session_u3 ON mobile_session (member_id) WHERE not revoked;

ALTER TABLE subscription ADD FOREIGN KEY (member_id) REFERENCES member(id);

CREATE UNIQUE INDEX sport_row_u1 ON sport_row (code);

ALTER TABLE club_event ADD FOREIGN KEY (trainer_id) REFERENCES trainer(id);
ALTER TABLE club_event ADD FOREIGN KEY (current_version_id) REFERENCES event_version(id);

ALTER TABLE event_version ADD FOREIGN KEY (event_id) REFERENCES club_event(id);
ALTER TABLE event_version ADD FOREIGN KEY (sport_id) REFERENCES sport_row(id);
CREATE UNIQUE INDEX event_version_u1 ON event_version (event_id, revision);
CREATE UNIQUE INDEX event_version_u2 ON event_version (event_id, id);

ALTER TABLE additional_sport ADD FOREIGN KEY (version_id) REFERENCES event_version(id);
ALTER TABLE additional_sport ADD FOREIGN KEY (sport_id) REFERENCES sport_row(id);
CREATE UNIQUE INDEX additional_sport_u1 ON additional_sport (version_id, sport_id);

ALTER TABLE program_day_row ADD FOREIGN KEY (version_id) REFERENCES event_version(id);
CREATE UNIQUE INDEX program_day_row_u1 ON program_day_row (version_id, ordinal);

ALTER TABLE program_slot ADD FOREIGN KEY (day_id) REFERENCES program_day_row(id);
CREATE UNIQUE INDEX program_slot_u1 ON program_slot (day_id, ordinal);

ALTER TABLE saved_event ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE saved_event ADD FOREIGN KEY (event_id) REFERENCES club_event(id);
CREATE UNIQUE INDEX saved_event_u1 ON saved_event (member_id, event_id);

ALTER TABLE follow ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE follow ADD FOREIGN KEY (trainer_id) REFERENCES trainer(id);
CREATE UNIQUE INDEX follow_u1 ON follow (member_id, trainer_id);

ALTER TABLE introduction_row ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE introduction_row ADD FOREIGN KEY (trainer_id) REFERENCES trainer(id);
CREATE UNIQUE INDEX introduction_row_u1 ON introduction_row (member_id, trainer_id);

ALTER TABLE trainer_link ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE trainer_link ADD FOREIGN KEY (trainer_id) REFERENCES trainer(id);
CREATE UNIQUE INDEX trainer_link_u1 ON trainer_link (member_id, trainer_id);

ALTER TABLE crew_row ADD FOREIGN KEY (trainer_id) REFERENCES trainer(id);
CREATE UNIQUE INDEX crew_row_u1 ON crew_row (trainer_id);

ALTER TABLE crew_mate ADD FOREIGN KEY (crew_id) REFERENCES crew_row(id);
ALTER TABLE crew_mate ADD FOREIGN KEY (member_id) REFERENCES member(id);
CREATE UNIQUE INDEX crew_mate_u1 ON crew_mate (crew_id, member_id);

ALTER TABLE hold_row ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE hold_row ADD FOREIGN KEY (event_id) REFERENCES club_event(id);
ALTER TABLE hold_row ADD FOREIGN KEY (version_id) REFERENCES event_version(id);

ALTER TABLE payment_row ADD FOREIGN KEY (hold_id) REFERENCES hold_row(id);
CREATE UNIQUE INDEX payment_row_u1 ON payment_row (hold_id);
CREATE UNIQUE INDEX payment_row_u2 ON payment_row (provider, reference);

ALTER TABLE payment_notice ADD FOREIGN KEY (payment_id) REFERENCES payment_row(id);
CREATE UNIQUE INDEX payment_notice_u1 ON payment_notice (provider, external_id);

ALTER TABLE refund_row ADD FOREIGN KEY (payment_id) REFERENCES payment_row(id);
CREATE UNIQUE INDEX refund_row_u1 ON refund_row (payment_id);

ALTER TABLE request_key ADD FOREIGN KEY (member_id) REFERENCES member(id);
CREATE UNIQUE INDEX request_key_u1 ON request_key (member_id, operation, key);

ALTER TABLE booking_row ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE booking_row ADD FOREIGN KEY (event_id) REFERENCES club_event(id);
ALTER TABLE booking_row ADD FOREIGN KEY (version_id) REFERENCES event_version(id);
ALTER TABLE booking_row ADD FOREIGN KEY (payment_id) REFERENCES payment_row(id);
CREATE UNIQUE INDEX booking_row_u1 ON booking_row (member_id, event_id) WHERE status = 'confirmed';
CREATE UNIQUE INDEX booking_row_u2 ON booking_row (payment_id);

ALTER TABLE pass_row ADD FOREIGN KEY (booking_id) REFERENCES booking_row(id);
CREATE UNIQUE INDEX pass_row_u1 ON pass_row (booking_id);
CREATE UNIQUE INDEX pass_row_u2 ON pass_row (nonce);

ALTER TABLE assignment ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE assignment ADD FOREIGN KEY (event_id) REFERENCES club_event(id);

ALTER TABLE rating_row ADD FOREIGN KEY (member_id) REFERENCES member(id);
ALTER TABLE rating_row ADD FOREIGN KEY (event_id) REFERENCES club_event(id);
CREATE UNIQUE INDEX rating_row_u1 ON rating_row (member_id, event_id);

ALTER TABLE route_point ADD FOREIGN KEY (event_id) REFERENCES club_event(id);
CREATE UNIQUE INDEX route_point_u1 ON route_point (event_id, ordinal);

ALTER TABLE location_share ADD FOREIGN KEY (event_id) REFERENCES club_event(id);
ALTER TABLE location_share ADD FOREIGN KEY (member_id) REFERENCES member(id);
CREATE UNIQUE INDEX location_share_u1 ON location_share (member_id, event_id) WHERE not revoked;

ALTER TABLE last_location ADD FOREIGN KEY (share_id) REFERENCES location_share(id);
CREATE UNIQUE INDEX last_location_u1 ON last_location (share_id);

ALTER TABLE club_event ADD FOREIGN KEY (id, current_version_id) REFERENCES event_version(event_id, id);
ALTER TABLE booking_row ADD FOREIGN KEY (event_id, version_id) REFERENCES event_version(event_id, id);
ALTER TABLE hold_row ADD FOREIGN KEY (event_id, version_id) REFERENCES event_version(event_id, id);
ALTER TABLE club_event ADD CHECK (capacity > 0 AND minimum_confirmed BETWEEN 1 AND capacity);
ALTER TABLE event_version ADD CHECK (ends > starts AND amount >= 0 AND currency ~ '^[A-Z]{3}$');
ALTER TABLE event_version ADD CHECK ((latitude IS NULL) = (longitude IS NULL) AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180);
ALTER TABLE rating_row ADD CHECK (stars BETWEEN 1 AND 5);
ALTER TABLE member ADD CHECK (status IN ('pending','active','rejected','suspended'));
CREATE INDEX event_schedule ON event_version (starts, id);
CREATE INDEX hold_capacity ON hold_row (event_id, expires);
CREATE INDEX otp_phone ON otp_challenge (phone, created);
CREATE INDEX booking_owner ON booking_row (member_id, event_id);
