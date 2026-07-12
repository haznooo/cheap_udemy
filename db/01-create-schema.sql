-- Clean up
DROP SCHEMA IF EXISTS public CASCADE;
CREATE SCHEMA public;

--  Ensure pg_trgm extension installed
CREATE EXTENSION IF NOT EXISTS pg_trgm;


CREATE TABLE users (
    user_id         SERIAL PRIMARY KEY,
    username        VARCHAR(20) NOT NULL UNIQUE,
    --          anonymize-on-delete trigger ('deleted_<id>@app.com' can reach 26+ character
    email           VARCHAR(254) NOT NULL UNIQUE,
    hashed_password VARCHAR(255) NULL,
    create_date     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    role            VARCHAR(20) NOT NULL DEFAULT 'student',

    CONSTRAINT valid_user_status CHECK (status IN ('active', 'deleted', 'banned', 'suspended')),
    CONSTRAINT valid_user_role CHECK (role IN ('student', 'instructor', 'admin')),

    CONSTRAINT valid_username_format CHECK (length(trim(username)) > 0),
    CONSTRAINT valid_email_format CHECK (email ~* '^.+@.+\..+$')
);
CREATE INDEX ix_user_email ON users(email);

CREATE TABLE users_profile (
    user_id      INT PRIMARY KEY,
    bio          TEXT NULL,
    avatar_url    VARCHAR(300) NULL,     -- avatar (filename from media upload)
    display_name VARCHAR(30) NULL,
    CONSTRAINT fk_user_profile FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE TABLE user_refresh_tokens (
    token_id          SERIAL PRIMARY KEY,
    user_id           INT NOT NULL,
    token_hash        VARCHAR(255) NOT NULL,
    device_info       VARCHAR(255) NULL,
    expires_at        TIMESTAMP with time zone NOT NULL,
    created_at        TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    revoked_at        TIMESTAMP with time zone NULL,
    is_used BOOLEAN NOT NULL DEFAULT FALSE,
    replaced_by_id INT NULL,
    chain_breached BOOLEAN NOT NULL DEFAULT FALSE,
    last_used_at TIMESTAMP with time zone NULL,
    ip_address VARCHAR(45) NULL,

    CONSTRAINT fk_user FOREIGN KEY(user_id) REFERENCES users(user_id) ON DELETE CASCADE
);
CREATE INDEX idx_refresh_tokens_user ON user_refresh_tokens(user_id);
-- Refresh tokens are stored as deterministic SHA-256 hashes and looked up by (user_id, token_hash);
-- this composite index makes that lookup a direct index seek.
CREATE INDEX idx_refresh_tokens_user_hash ON user_refresh_tokens(user_id, token_hash);

CREATE TABLE categories (
    category_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    slug VARCHAR(100) NOT NULL UNIQUE,
    parent_id INT NULL,
    CONSTRAINT fk_category_parent FOREIGN KEY (parent_id) REFERENCES categories(category_id) ON DELETE SET NULL
);

-- 7) Courses Table
CREATE TABLE courses (
    course_id SERIAL PRIMARY KEY,
    category_id INT NOT NULL,
    instructor_id INT NOT NULL,
    title VARCHAR(150) NOT NULL,
    code VARCHAR(30) NOT NULL,
    description TEXT,
    thumbnail_url VARCHAR(300) NULL,
    price DECIMAL(10, 2) NOT NULL DEFAULT 0.00,
    status VARCHAR(50) NOT NULL DEFAULT 'draft',
    level VARCHAR(50) NOT NULL DEFAULT 'beginner',
    removal_reason TEXT,
    deleted_at TIMESTAMP NULL,
    estimated_duration_minutes INT NOT NULL DEFAULT 0,
    avg_rating DECIMAL(3, 2) NOT NULL DEFAULT 0.00,
    reviews_count INT NOT NULL DEFAULT 0,
    course_metadata JSONB NOT NULL DEFAULT '{ "lessons_count": 0, "enrollments_count": 0 }'::jsonb,
    created_date TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    published_date TIMESTAMP  with time zone,
    updated_at TIMESTAMP  with time zone  DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT ck_courses_price CHECK (price >= 0),
    CONSTRAINT ck_courses_duration CHECK (estimated_duration_minutes >= 0),
    CONSTRAINT ck_courses_rating CHECK (avg_rating >= 0 AND avg_rating <= 5),
    CONSTRAINT ck_courses_review_count CHECK (reviews_count >= 0),
    CONSTRAINT valid_course_status CHECK (status IN ('draft', 'published', 'retired')),
    CONSTRAINT valid_course_level CHECK (level IN ('beginner', 'intermediate', 'advanced')),
    CONSTRAINT fk_courses_instructors FOREIGN KEY (instructor_id) REFERENCES users(user_id),
    CONSTRAINT fk_courses_categories FOREIGN KEY (category_id) REFERENCES categories(category_id)
);
CREATE INDEX ix_courses_title_fuzzy ON courses USING GIN ("title" gin_trgm_ops);
CREATE INDEX ix_course ON courses(code);
CREATE INDEX ix_courses_instructor_id ON courses (instructor_id);
CREATE INDEX ix_courses_category_id ON courses (category_id);
-- NEW: the public course list should only show published, non-deleted courses;
--      this partial index makes that filtered read cheap.
CREATE INDEX ix_courses_published ON courses (status) WHERE deleted_at IS NULL;


CREATE TABLE sections (
    section_id SERIAL PRIMARY KEY,
    course_id INT NOT NULL,
    title VARCHAR(200) NOT NULL DEFAULT 'Main',
    sort_order INT NOT NULL,
    CONSTRAINT fk_sections_courses FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE CASCADE,

    CONSTRAINT ck_sort_order CHECK (sort_order > 0),
    -- CHANGED: was UNIQUE (sort_order) which is GLOBAL — it stopped two different
    --          courses from each having a section ordered 1. Ordering is only
    --          meaningful *within* a course, so scope the uniqueness per course.
    CONSTRAINT uq_section_order_per_course UNIQUE (course_id, sort_order)
);


CREATE TABLE lessons (
    lesson_id SERIAL PRIMARY KEY,
    section_id INT NOT NULL,
    title VARCHAR(200) NOT NULL,
    content_blocks JSONB NOT NULL DEFAULT '[]'::jsonb,
    lesson_metadata JSONB NOT NULL DEFAULT '{ "video_duration": 0, "quiz_count": 0, "word_count": 0 }'::jsonb,
    sort_order INT NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'draft',
    estimated_duration_minutes INT NOT NULL DEFAULT 0,
    created_at TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP with time zone null DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT valid_lesson_status CHECK (status IN ('draft', 'published', 'hidden')),
    -- NEW: lessons had no ordering uniqueness at all while sections did — make
    --      them consistent (ordering unique within a section).
    CONSTRAINT uq_lesson_order_per_section UNIQUE (section_id, sort_order),
    CONSTRAINT fk_lessons_sections FOREIGN KEY (section_id) REFERENCES sections(section_id) ON DELETE CASCADE
);

CREATE TABLE enrollments (
    enrollment_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    course_id INT NOT NULL,
    enrollment_date TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completion_date TIMESTAMP NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'active',
    progress_percentage DECIMAL(5, 2) NOT NULL DEFAULT 0.00,

    CONSTRAINT valid_enrollment_status CHECK (status IN ('active', 'completed', 'dropped', 'suspended')),
    CONSTRAINT uq_enrollments_user_course UNIQUE (user_id, course_id),
    CONSTRAINT ck_enrollments_progress CHECK (progress_percentage >= 0 AND progress_percentage <= 100),
    CONSTRAINT fk_enrollments_users FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    CONSTRAINT fk_enrollments_courses FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE RESTRICT
);
CREATE INDEX ix_enrollments_course_id ON enrollments (course_id);
CREATE INDEX ix_enrollments_user_id ON enrollments (user_id);

CREATE TABLE user_lesson_progress (
    user_id INT,
    lesson_id INT,
    is_completed BOOLEAN DEFAULT FALSE,
    completed_at TIMESTAMP with time zone DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, lesson_id),
    CONSTRAINT fk_user_Lesson_progress_users FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    CONSTRAINT fk_user_Lesson_progress_lessons FOREIGN KEY (lesson_id) REFERENCES lessons(lesson_id) ON DELETE CASCADE
);

CREATE TABLE reviews (
    review_id SERIAL PRIMARY KEY,
    course_id INT NOT NULL,
    user_id INT NOT NULL,
    rating SMALLINT NULL CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- NEW: you already expose an "update review" endpoint, but there was no way
    --      to tell an edited review from a fresh one. Set this in the UPDATE path.
    updated_at TIMESTAMP with time zone NULL,
    CONSTRAINT uq_user_course_review UNIQUE (user_id, course_id),
    CONSTRAINT fk_review_course FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE CASCADE,
    CONSTRAINT fk_review_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);
-- NEW: rating aggregation reads reviews by course; index supports the trigger + listing.
CREATE INDEX ix_reviews_course_id ON reviews (course_id);

CREATE TABLE login_logs (
    id SERIAL PRIMARY KEY,
    -- CHANGED: was INT NOT NULL with an FK — which means a failed login for an
    --          *unknown* email (the most interesting case: credential stuffing,
    --          typo-squatting) could not be recorded at all. Make it nullable...
    user_id INT NULL,
    -- NEW: ...and store what the caller actually typed so unmatched attempts are
    --      still auditable. Keep it to the identifier only (never the password).
    attempted_identifier VARCHAR(254) NULL,
    ip_address INET,
    user_agent TEXT,
    status VARCHAR(50) NOT NULL,
    attempted_at TIMESTAMP with time zone DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT valid_login_status CHECK (status IN ('success', 'failed', 'locked')),
    -- CHANGED: FK now tolerates NULL (PostgreSQL skips the FK check when NULL).
    CONSTRAINT fk_login_logs_users FOREIGN KEY (user_id) REFERENCES users(user_id)
);
CREATE INDEX ix_login_Logs_attempted_at ON login_logs(attempted_at);

CREATE TABLE admin_actions (
    id SERIAL PRIMARY KEY,
    admin_id INT NOT NULL,
    target_table VARCHAR(50) NOT NULL,
    target_id INT NOT NULL,
    old_value JSONB,
    new_value JSONB,
    performed_at TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    action_type VARCHAR(50) NOT NULL,

    CONSTRAINT valid_action_type CHECK (action_type IN ('create', 'update', 'delete', 'ban', 'unban', 'suspend', 'unsuspend')),
    CONSTRAINT fk_admin_actions_users FOREIGN KEY (admin_id) REFERENCES users(user_id)
);
CREATE INDEX ix_admin_actions_admin_id ON admin_actions (admin_id);
CREATE INDEX ix_admin_actions_performed_at ON admin_actions(performed_at);

-- 15) payments
-- CHANGED: the original payments table only had (amount, user_id) — there was no
--          way to know *what* was paid for. Even as a pure simulation (no real
--          gateway), a payment needs to point at a course so "buy -> enroll"
--          tells a coherent story. Provider fields default to a fake/simulated
--          value so you never have to integrate Stripe etc.
CREATE TABLE payments (
    payment_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    course_id INT NOT NULL,                                  -- NEW: what was bought
    enrollment_id INT NULL,                                  -- NEW: the enrollment it produced (if any)
    amount NUMERIC(10, 2) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'USD',                 -- NEW
    status VARCHAR(20) NOT NULL DEFAULT 'pending',           -- NEW: simulate states
    provider VARCHAR(50) NOT NULL DEFAULT 'simulated',       -- NEW
    provider_reference VARCHAR(100) NULL,                    -- NEW: fake txn id
    payment_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT ck_payments_amount CHECK (amount >= 0),
    CONSTRAINT valid_payment_status CHECK (status IN ('pending', 'completed', 'failed', 'refunded')),
    CONSTRAINT fk_payments_users FOREIGN KEY (user_id) REFERENCES users(user_id),
    CONSTRAINT fk_payments_courses FOREIGN KEY (course_id) REFERENCES courses(course_id),
    CONSTRAINT fk_payments_enrollments FOREIGN KEY (enrollment_id) REFERENCES enrollments(enrollment_id) ON DELETE SET NULL
);
CREATE INDEX ix_payments_user_id ON payments (user_id);
CREATE INDEX ix_payments_course_id ON payments (course_id);

-- ============================================================================
--  Triggers
-- ============================================================================

-- Course publish/update bookkeeping (unchanged)
CREATE OR REPLACE FUNCTION handle_course_publication()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.status = 'published' AND OLD.status = 'draft' THEN
        NEW.published_date = NOW();
    END IF;
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_course_publish_logic
    BEFORE UPDATE ON courses
    FOR EACH ROW
    EXECUTE FUNCTION handle_course_publication();

-- Verify Instructor (unchanged)
CREATE OR REPLACE FUNCTION verify_instructor_role()
RETURNS TRIGGER AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM users
        WHERE user_id = NEW.instructor_id
        AND role IN ('instructor', 'admin')
    ) THEN
        RAISE EXCEPTION 'User % is not authorized to be an instructor.', NEW.instructor_id;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_verify_instructor
    BEFORE INSERT OR UPDATE ON courses
    FOR EACH ROW
    EXECUTE FUNCTION verify_instructor_role();

-- Verify Admin (unchanged)
CREATE OR REPLACE FUNCTION verify_admin_privileges()
RETURNS TRIGGER AS $$
DECLARE
    u_role VARCHAR(50);
BEGIN
    SELECT role INTO u_role FROM users WHERE user_id = NEW.admin_id;
    IF u_role IS NULL OR u_role != 'admin' THEN
        RAISE EXCEPTION 'Access Denied: User % is not an authorized administrator.', NEW.admin_id;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_verify_admin_action
    BEFORE INSERT ON admin_actions
    FOR EACH ROW
    EXECUTE FUNCTION verify_admin_privileges();

--  Sync Progress (unchanged)
CREATE OR REPLACE FUNCTION update_enrollment_progress()
RETURNS TRIGGER AS $$
DECLARE
    total_lessons INT;
    completed_lessons INT;
    v_course_id INT;
BEGIN
    SELECT s.course_id INTO v_course_id
    FROM lessons l
    JOIN sections s ON l.section_id = s.section_id
    WHERE l.lesson_id = NEW.lesson_id;

    SELECT COUNT(*) INTO total_lessons
    FROM sections s
    JOIN lessons l ON s.section_id = l.section_id
    WHERE s.course_id = v_course_id;

    SELECT COUNT(*) INTO completed_lessons
    FROM user_lesson_progress ulp
    JOIN lessons l ON ulp.lesson_id = l.lesson_id
    JOIN sections s ON l.section_id = s.section_id
    WHERE ulp.user_id = NEW.user_id
    AND s.course_id = v_course_id
    AND ulp.is_completed = TRUE;

    IF total_lessons > 0 THEN
        UPDATE enrollments
        SET progress_percentage = (completed_lessons::FLOAT / total_lessons::FLOAT) * 100
        WHERE user_id = NEW.user_id AND course_id = v_course_id;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_sync_progress
    AFTER INSERT OR UPDATE ON user_lesson_progress
    FOR EACH ROW
    EXECUTE FUNCTION update_enrollment_progress();

-- Immutable Audit Log (unchanged)
CREATE OR REPLACE FUNCTION protect_audit_log()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Table % is immutable.', TG_TABLE_NAME;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_lock_admin_actions
    BEFORE UPDATE OR DELETE ON admin_actions
    FOR EACH ROW
    EXECUTE FUNCTION protect_audit_log();

-- delete user TRIGGER (unchanged)
CREATE OR REPLACE FUNCTION anonymize_user_on_delete()
RETURNS TRIGGER AS $$
BEGIN
    DELETE FROM users_profile WHERE user_id = OLD.user_id;
    DELETE FROM user_refresh_tokens WHERE user_id = OLD.user_id;

    UPDATE users
    SET
        username = 'deleted_' || OLD.user_id,
        email = 'deleted_' || OLD.user_id || '@app.com',  -- now fits thanks to VARCHAR(254)
        hashed_password = NULL,
        status = 'deleted'
    WHERE user_id = OLD.user_id;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;
CREATE TRIGGER trg_anonymize_user
    BEFORE DELETE ON users
    FOR EACH ROW
    EXECUTE FUNCTION anonymize_user_on_delete();

-- ----------------------------------------------------------------------------
-- NEW: keep courses.avg_rating / review_count in sync with the reviews table.
--          Without this, a rating shown on a course card would have to be
--          aggregated on every read. Fires on any review insert/update/delete.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION sync_course_rating()
RETURNS TRIGGER AS $$
DECLARE
    v_course_id INT;
BEGIN
    v_course_id := COALESCE(NEW.course_id, OLD.course_id);

    UPDATE courses
    SET avg_rating   = COALESCE((SELECT ROUND(AVG(rating)::numeric, 2)
                                 FROM reviews WHERE course_id = v_course_id), 0),
        reviews_count = (SELECT COUNT(*) FROM reviews WHERE course_id = v_course_id)
    WHERE course_id = v_course_id;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_sync_course_rating
    AFTER INSERT OR UPDATE OR DELETE ON reviews
    FOR EACH ROW
    EXECUTE FUNCTION sync_course_rating();

-- ----------------------------------------------------------------------------
--  NEW (OPTIONAL): keep the denormalized counters in course_metadata correct.
--      You said you're unsure these are worth keeping. They already exist in the
--      schema and the app builds them, so rather than let them sit stale at 0,
--      this maintains them. If you'd rather just compute counts on read, drop
--      these two triggers and the columns from course_metadata.
-- ----------------------------------------------------------------------------

-- enrollments_count
CREATE OR REPLACE FUNCTION sync_enrollments_count()
RETURNS TRIGGER AS $$
DECLARE
    v_course_id INT;
BEGIN
    v_course_id := COALESCE(NEW.course_id, OLD.course_id);

    UPDATE courses
    SET course_metadata = jsonb_set(
            course_metadata,
            '{enrollments_count}',
            to_jsonb((SELECT COUNT(*) FROM enrollments WHERE course_id = v_course_id))
        )
    WHERE course_id = v_course_id;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_sync_enrollments_count
    AFTER INSERT OR DELETE ON enrollments
    FOR EACH ROW
    EXECUTE FUNCTION sync_enrollments_count();

-- lessons_count (lessons -> sections -> course)
CREATE OR REPLACE FUNCTION sync_lessons_count()
RETURNS TRIGGER AS $$
DECLARE
    v_section_id INT;
    v_course_id INT;
BEGIN
    v_section_id := COALESCE(NEW.section_id, OLD.section_id);
    SELECT course_id INTO v_course_id FROM sections WHERE section_id = v_section_id;

    IF v_course_id IS NOT NULL THEN
        UPDATE courses
        SET course_metadata = jsonb_set(
                course_metadata,
                '{lessons_count}',
                to_jsonb((
                    SELECT COUNT(*)
                    FROM lessons l
                    JOIN sections s ON l.section_id = s.section_id
                    WHERE s.course_id = v_course_id
                ))
            )
        WHERE course_id = v_course_id;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_sync_lessons_count
    AFTER INSERT OR DELETE ON lessons
    FOR EACH ROW
    EXECUTE FUNCTION sync_lessons_count();

-- Revokes an entire refresh-token chain (breach response): walks replaced_by_id forward from
-- start_id via a recursive CTE and flips chain_breached/is_used/revoked_at for every linked
-- token in one statement, instead of the app fetching+updating one hop at a time.
CREATE OR REPLACE PROCEDURE revoke_breached_chain(start_id integer)
LANGUAGE plpgsql
AS $$
BEGIN
    WITH RECURSIVE chain AS (
        SELECT token_id, replaced_by_id
        FROM user_refresh_tokens
        WHERE token_id = start_id

        UNION ALL

        SELECT t.token_id, t.replaced_by_id
        FROM user_refresh_tokens t
        JOIN chain c ON t.token_id = c.replaced_by_id
    )
    UPDATE user_refresh_tokens
    SET chain_breached = true,
        is_used = true,
        revoked_at = COALESCE(revoked_at, now())
    WHERE token_id IN (SELECT token_id FROM chain);
END;
$$;