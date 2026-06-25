-- 1) Clean up
DROP SCHEMA IF EXISTS public CASCADE;
CREATE SCHEMA public;

-- 2) Ensure pg_trgm extension installed
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- 3) create tables
CREATE TABLE countries (
    country_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    iso_code CHAR(2) UNIQUE NOT NULL
);

CREATE TABLE users (
    user_id         SERIAL PRIMARY KEY,                
    username        VARCHAR(20) NOT NULL UNIQUE,
    email           VARCHAR(20) NOT NULL UNIQUE,
    hashed_password VARCHAR(255) NULL,
    create_date     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
--	deleted_date    TIMESTAMP NULL ,
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
    image_url    VARCHAR(300) NULL,                      
    country_id   INT NULL,
    display_name VARCHAR(30) NULL,
    CONSTRAINT fk_user_profile FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    CONSTRAINT fk_user_country FOREIGN KEY (country_id) REFERENCES countries(country_id) 
);
CREATE TABLE user_refresh_tokens (
    token_id          SERIAL PRIMARY KEY,
    user_id           INT NOT NULL,
    token_hash        VARCHAR(255) NOT NULL, 
    device_info       VARCHAR(255) NULL,     
    expires_at        TIMESTAMP NOT NULL,   
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    revoked_at        TIMESTAMP NULL,       
    is_used BOOLEAN NOT NULL DEFAULT FALSE, 
    replaced_by_id INT NULL, -- Points to the new token_id that replaced this one
    chain_breached BOOLEAN NOT NULL DEFAULT FALSE, -- if there are mulitple attempts to use this expired token then ignore it 
    last_used_at TIMESTAMP NULL ,
    ip_address VARCHAR(45) NULL, -- Supports both IPv4 and IPv6

    CONSTRAINT fk_user FOREIGN KEY(user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- Index for blazing fast lookups when validating a token
CREATE INDEX idx_refresh_tokens_user ON user_refresh_tokens(user_id);
-- 6) categories Table
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
    price DECIMAL(10, 2) NOT NULL DEFAULT 0.00, 
    status VARCHAR(50) NOT NULL DEFAULT 'draft', 
    level VARCHAR(50) NOT NULL DEFAULT 'beginner',
    removal_reason TEXT,                  
    deleted_at TIMESTAMP NULL,          
    estimated_duration_minutes INT NOT NULL DEFAULT 0, 
    course_metadata JSONB NOT NULL DEFAULT '{ "lessons_count": 0, "enrollments_count": 0 }'::jsonb,
    created_date TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    published_date TIMESTAMP  with time zone,
    updated_at TIMESTAMP  with time zone  DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT ck_courses_price CHECK (price >= 0),
    CONSTRAINT ck_courses_duration CHECK (estimated_duration_minutes >= 0),
    CONSTRAINT valid_course_status CHECK (status IN ('draft', 'published', 'retired')),
    CONSTRAINT valid_course_level CHECK (level IN ('beginner', 'intermediate', 'advanced')),
    CONSTRAINT fk_courses_instructors FOREIGN KEY (instructor_id) REFERENCES users(user_id),
    CONSTRAINT fk_courses_categories FOREIGN KEY (category_id) REFERENCES categories(category_id)
);
CREATE INDEX ix_courses_title_fuzzy ON courses USING GIN ("title" gin_trgm_ops);
CREATE INDEX ix_course ON courses(code);
CREATE INDEX ix_courses_instructor_id ON courses (instructor_id);
CREATE INDEX ix_courses_category_id ON courses (category_id);

-- 8) Sections Table
CREATE TABLE sections (
    section_id SERIAL PRIMARY KEY,
    course_id INT NOT NULL,
    title VARCHAR(200) NOT NULL DEFAULT 'Main',
    sort_order INT NOT NULL ,
    CONSTRAINT fk_sections_courses FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE CASCADE,

	    CONSTRAINT ck_sort_order CHECK (sort_order > 0),
		Constraint uq_sort_order UNIQUE (sort_order)
);

-- 9) Lessons Table
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
    CONSTRAINT fk_lessons_sections FOREIGN KEY (section_id) REFERENCES sections(section_id) ON DELETE CASCADE
);

-- 10) Enrollments
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

-- 11) Lesson Progress
CREATE TABLE user_lesson_progress (
    user_id INT,
    lesson_id INT,
    is_completed BOOLEAN DEFAULT FALSE,
    completed_at TIMESTAMP with time zone DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, lesson_id),
    CONSTRAINT fk_user_Lesson_progress_users FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    CONSTRAINT fk_user_Lesson_progress_lessons FOREIGN KEY (lesson_id) REFERENCES lessons(lesson_id) ON DELETE CASCADE
);

-- 12) Reviews
CREATE TABLE reviews (
    review_id SERIAL PRIMARY KEY,
    course_id INT NOT NULL,
    user_id INT NOT NULL,
    rating SMALLINT NULL CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_user_course_review UNIQUE (user_id, course_id),
    CONSTRAINT fk_review_course FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE CASCADE,
    CONSTRAINT fk_review_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 13) Login Logs
CREATE TABLE login_logs (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    ip_address INET, 
    user_agent TEXT, 
    status VARCHAR(50) NOT NULL,
    attempted_at TIMESTAMP with time zone DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT valid_login_status CHECK (status IN ('success', 'failed', 'locked')),
    CONSTRAINT fk_login_logs_users FOREIGN KEY (user_id) REFERENCES users(user_id)
);
CREATE INDEX ix_login_Logs_attempted_at ON login_logs(attempted_at);

-- 14) Admin Actions
CREATE TABLE admin_actions (
    id SERIAL PRIMARY KEY,
    admin_id INT NOT NULL,
    target_table VARCHAR(50) NOT NULL,
    target_id INT NOT NULL,
    old_value JSONB, 
    new_value JSONB,    
    performed_at TIMESTAMP with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    action_type VARCHAR(50) NOT NULL,
    
    CONSTRAINT valid_action_type CHECK (action_type IN ('create', 'update', 'delete', 'ban', 'unban')),
    CONSTRAINT fk_admin_actions_users FOREIGN KEY (admin_id) REFERENCES users(user_id)
);
CREATE INDEX ix_admin_actions_admin_id ON admin_actions (admin_id);
CREATE INDEX ix_admin_actions_performed_at ON admin_actions(performed_at);

-- 15) payments
CREATE TABLE payments (
    payment_id SERIAL PRIMARY KEY,
    amount NUMERIC(10, 2) NOT NULL, 
    payment_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, 
    user_id INT NOT NULL, 
    CONSTRAINT fk_payments_users FOREIGN KEY (user_id) REFERENCES users(user_id)
);

-- 16) Triggers
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

-- 17) Verify Instructor
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
    
-- 18) Verify Admin
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
    
-- 19) Sync Progress
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

-- 20) Immutable Audit Log
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

-- 21) delete user TRIGGER
CREATE OR REPLACE FUNCTION anonymize_user_on_delete()
RETURNS TRIGGER AS $$
BEGIN
    -- 1. Manually handle the profile delete since we are stopping the user cascade
    DELETE FROM users_profile WHERE user_id = OLD.user_id;
	delete from user_refresh_tokens where user_id =  OLD.user_id;

    -- 2. anonymized details
    UPDATE users 
    SET 
        username = 'deleted_' || OLD.user_id,
        email = 'deleted_' || OLD.user_id || '@app.com',
        hashed_password = NULL, -- Clear the password since it's nullable!
        status = 'deleted'     -- Change status using your valid CHECK constraint
	--	deleted_date = CURRENT_TIMESTAMP
    WHERE user_id = OLD.user_id;

    -- 3. CRITICAL: Returning NULL cancels the actual row deletion
    RETURN null; 
END;
$$ LANGUAGE plpgsql;
CREATE TRIGGER trg_anonymize_user
before delete ON users
FOR EACH ROW
EXECUTE FUNCTION anonymize_user_on_delete();