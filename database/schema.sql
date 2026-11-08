-- =============================================================================
-- CodeQuest — database/schema.sql
-- SQLite DDL for the CodeQuest quiz system: 8 tables in Third Normal Form (3NF).
--
-- Phase   : P1 — Database Schema, Seed Data & 3NF Documentation
-- Engine  : SQLite 3 (file-based, no server; see docs/codequest-proposal.md line 19)
-- Style   : plain, readable SQL — this file is explained in the instructor demo,
--           so every constraint carries a short comment saying what it protects.
--
-- How to create the database (from the repository root):
--     sqlite3 codequest.db < database/schema.sql
--     sqlite3 codequest.db < database/seed.sql
--
-- IMPORTANT — foreign keys are OFF by default in SQLite
--   (https://sqlite.org/foreignkeys.html section 2). The PRAGMA below only
--   affects the connection that runs this script. The application must run
--   "PRAGMA foreign_keys = ON;" on EVERY connection it opens (phase P4,
--   Data/Db.cs) or referential integrity is not actually enforced.
-- =============================================================================

PRAGMA foreign_keys = ON;


-- -----------------------------------------------------------------------------
-- users — the local account(s) of the application.
-- Plan assumption #1: a single local user; the login form validates against
-- this table. Multi-role teacher/student separation is out of scope.
-- -----------------------------------------------------------------------------
CREATE TABLE users (
    user_id       INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT    NOT NULL UNIQUE,          -- login name, e.g. 'demo'
    password_hash TEXT    NOT NULL,                 -- 'pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>' - PBKDF2-HMAC-SHA256, 600,000 iterations (OWASP 2026 guidance; implemented in P7 via Rfc2898DeriveBytes); the plaintext is NEVER stored
    display_name  TEXT    NOT NULL,                 -- shown in the UI ("Demo Student")
    created_at    TEXT    NOT NULL                  -- ISO-8601 UTC, e.g. '2026-11-01T09:00:00Z'
);


-- -----------------------------------------------------------------------------
-- topics — the subject areas a quiz can belong to.
-- One row per subject (C#, Python, SQL, Networking, Web Security, ...).
-- 'category' groups topics for the reporting screens (Programming vs Cybersecurity).
-- -----------------------------------------------------------------------------
CREATE TABLE topics (
    topic_id    INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT    NOT NULL UNIQUE,             -- e.g. 'C#', 'Web Security'
    category    TEXT    NOT NULL CHECK (category IN ('Programming', 'Cybersecurity')),
    description TEXT    NOT NULL,                    -- one line shown in the topic picker
    created_at  TEXT    NOT NULL                     -- ISO-8601 UTC
);


-- -----------------------------------------------------------------------------
-- quizzes — a quiz header: one topic, one difficulty, one review status.
--
-- status is the answer-key review workflow (proposal line 23):
--   'Draft'    — created by the AI or by hand; NOT takeable by a student.
--   'Approved' — the answer key has been reviewed; the quiz is takeable.
-- The CHECK constraint makes any other status impossible at the storage layer.
--
-- ON DELETE topic_id = RESTRICT: a topic that still has quizzes must not be
-- deleted. The delete is blocked with a friendly message in the UI (P8)
-- instead of silently orphaning the quizzes.
-- -----------------------------------------------------------------------------
CREATE TABLE quizzes (
    quiz_id    INTEGER PRIMARY KEY AUTOINCREMENT,
    topic_id   INTEGER NOT NULL,
    title      TEXT    NOT NULL,
    difficulty TEXT    NOT NULL CHECK (difficulty IN ('Easy', 'Medium', 'Hard')),
    status     TEXT    NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft', 'Approved')),
    created_at TEXT    NOT NULL,                    -- ISO-8601 UTC

    CONSTRAINT fk_quizzes_topic
        FOREIGN KEY (topic_id) REFERENCES topics (topic_id) ON DELETE RESTRICT,

    -- A topic cannot hold two quizzes with the same title (P8 duplicate check).
    CONSTRAINT uq_quizzes_topic_title UNIQUE (topic_id, title)
);

-- (indexes are created in one block at the end of this file)


-- -----------------------------------------------------------------------------
-- questions — the questions of one quiz.
-- 'position' is the display order inside the quiz (1, 2, 3, ...) and is unique
-- per quiz, so the quiz player always shows the questions in a stable order.
--
-- ON DELETE quiz_id = CASCADE: a question only makes sense inside its quiz.
-- When a quiz that was never attempted is deleted, its questions (and their
-- choices) go with it — there would be nothing left to show.
-- A quiz that HAS attempts cannot be deleted at all: quiz_attempts.quiz_id
-- is RESTRICT, so the historical record protects the quiz.
-- -----------------------------------------------------------------------------
CREATE TABLE questions (
    question_id   INTEGER PRIMARY KEY AUTOINCREMENT,
    quiz_id       INTEGER NOT NULL,
    question_text TEXT    NOT NULL,
    explanation   TEXT    NOT NULL,                  -- shown in answer review (P10) and review screen (P13)
    position      INTEGER NOT NULL CHECK (position >= 1),
    created_at    TEXT    NOT NULL,                  -- ISO-8601 UTC

    CONSTRAINT fk_questions_quiz
        FOREIGN KEY (quiz_id) REFERENCES quizzes (quiz_id) ON DELETE CASCADE,

    -- No two questions of the same quiz may share a position.
    CONSTRAINT uq_questions_quiz_position UNIQUE (quiz_id, position)
);


-- -----------------------------------------------------------------------------
-- choices — the answer options of one question (four per question).
-- 'is_correct' is the answer key: exactly one row per question has is_correct = 1.
-- It lives HERE and not in submitted_answers, because the key is a property of
-- the question, not of one student's answer (see docs/normalization.md, 2NF).
--
-- ON DELETE question_id = CASCADE: a choice only makes sense inside its
-- question, so removing the question removes its options.
--
-- The extra UNIQUE (question_id, choice_id) is the parent key of the composite
-- foreign key used by submitted_answers; it lets the database prove that the
-- chosen choice really belongs to the answered question.
-- -----------------------------------------------------------------------------
CREATE TABLE choices (
    choice_id   INTEGER PRIMARY KEY AUTOINCREMENT,
    question_id INTEGER NOT NULL,
    choice_text TEXT    NOT NULL,
    is_correct  INTEGER NOT NULL DEFAULT 0 CHECK (is_correct IN (0, 1)),
    position    INTEGER NOT NULL CHECK (position >= 1),

    CONSTRAINT fk_choices_question
        FOREIGN KEY (question_id) REFERENCES questions (question_id) ON DELETE CASCADE,

    -- The same option text cannot be used twice inside one question.
    CONSTRAINT uq_choices_question_text UNIQUE (question_id, choice_text),

    -- Parent key for the composite FK in submitted_answers (same-question rule).
    CONSTRAINT uq_choices_question_choice UNIQUE (question_id, choice_id)
);


-- -----------------------------------------------------------------------------
-- quiz_attempts — one row per completed quiz run by one user.
-- 'score' is the number of CORRECT answers (not a percentage). The number of
-- answered questions is derived by counting the rows of submitted_answers, so
-- no redundant "total questions" column is stored (see docs/normalization.md).
--
-- ON DELETE quiz_id = RESTRICT: a quiz that has been attempted is history and
-- must not be deleted. The delete is blocked with a friendly message in the UI.
-- ON DELETE user_id = RESTRICT: the same rule protects the user's own history.
-- -----------------------------------------------------------------------------
CREATE TABLE quiz_attempts (
    attempt_id   INTEGER PRIMARY KEY AUTOINCREMENT,
    quiz_id      INTEGER NOT NULL,
    user_id      INTEGER NOT NULL,
    score        INTEGER NOT NULL CHECK (score >= 0),   -- number of correct answers
    attempted_at TEXT    NOT NULL,                      -- ISO-8601 UTC

    CONSTRAINT fk_quiz_attempts_quiz
        FOREIGN KEY (quiz_id) REFERENCES quizzes (quiz_id) ON DELETE RESTRICT,

    CONSTRAINT fk_quiz_attempts_user
        FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


-- -----------------------------------------------------------------------------
-- submitted_answers — what the student actually picked, one row per answered
-- question of one attempt.
--
-- Primary key (attempt_id, question_id): a question can be answered once per
-- attempt, and the same question may be answered in many different attempts.
--
-- ON DELETE attempt_id = CASCADE: an answer row only makes sense inside its
-- attempt, so deleting the attempt removes its answers.
-- ON DELETE question_id = RESTRICT and ON DELETE chosen_choice_id = RESTRICT:
-- an answered question and the option that was picked are part of the history
-- and must not disappear while the answer still points at them.
-- The composite FK (question_id, chosen_choice_id) proves that the chosen
-- option belongs to the answered question — a student can never record a
-- choice from a different question.
-- -----------------------------------------------------------------------------
CREATE TABLE submitted_answers (
    attempt_id       INTEGER NOT NULL,
    question_id      INTEGER NOT NULL,
    chosen_choice_id INTEGER NOT NULL,

    PRIMARY KEY (attempt_id, question_id),

    CONSTRAINT fk_submitted_answers_attempt
        FOREIGN KEY (attempt_id) REFERENCES quiz_attempts (attempt_id) ON DELETE CASCADE,

    CONSTRAINT fk_submitted_answers_question
        FOREIGN KEY (question_id) REFERENCES questions (question_id) ON DELETE RESTRICT,

    CONSTRAINT fk_submitted_answers_choice
        FOREIGN KEY (chosen_choice_id) REFERENCES choices (choice_id) ON DELETE RESTRICT,

    CONSTRAINT fk_submitted_answers_choice_question
        FOREIGN KEY (question_id, chosen_choice_id)
        REFERENCES choices (question_id, choice_id) ON DELETE RESTRICT
);


-- -----------------------------------------------------------------------------
-- user_achievements — one row per unlocked achievement of one user
-- (gamification, plan WP1 / acceptance A5).
--
-- The catalog itself (key, title, description, glyph, rule) is defined in the
-- code (Services/AchievementEvaluator.cs); this table records only WHO
-- unlocked WHAT and WHEN. Nothing is ever updated: an unlock happens once.
--
-- Primary key (user_id, achievement_key): an achievement unlocks ONCE per
-- user — re-completing the same feat must never duplicate the row (A5). The
-- service layer writes with INSERT OR IGNORE, so a repeated unlock is a no-op
-- instead of an error.
--
-- ON DELETE user_id = CASCADE: an unlock record only means something for its
-- user, so when an account is deleted its unlock records go with it. (This is
-- deliberately different from quiz_attempts.user_id = RESTRICT: attempts are
-- the historical record the screens must keep readable, while an achievement
-- row of a deleted account would be an orphan with no owner to show it.)
-- unlocked_at is ISO-8601 UTC text, the same format as every other timestamp.
-- -----------------------------------------------------------------------------
CREATE TABLE user_achievements (
    user_id         INTEGER NOT NULL,
    achievement_key TEXT    NOT NULL,
    unlocked_at     TEXT    NOT NULL,                  -- ISO-8601 UTC

    PRIMARY KEY (user_id, achievement_key),

    CONSTRAINT fk_user_achievements_user
        FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
);


-- =============================================================================
-- Indexes
-- SQLite requires (or strongly recommends) an index on every foreign key
-- column that is used as a child reference, otherwise every parent delete or
-- join has to scan the whole child table
-- (https://sqlite.org/foreignkeys.html section 3).
-- One index per child FK column:
-- =============================================================================
CREATE INDEX idx_quizzes_topic_id              ON quizzes (topic_id);
CREATE INDEX idx_questions_quiz_id             ON questions (quiz_id);
CREATE INDEX idx_choices_question_id           ON choices (question_id);
CREATE INDEX idx_quiz_attempts_quiz_id         ON quiz_attempts (quiz_id);
CREATE INDEX idx_quiz_attempts_user_id         ON quiz_attempts (user_id);
CREATE INDEX idx_submitted_answers_attempt_id  ON submitted_answers (attempt_id);
CREATE INDEX idx_submitted_answers_question_id ON submitted_answers (question_id);
CREATE INDEX idx_submitted_answers_choice_id   ON submitted_answers (chosen_choice_id);
CREATE INDEX idx_user_achievements_user_id     ON user_achievements (user_id);
