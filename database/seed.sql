-- =============================================================================
-- CodeQuest — database/seed.sql
-- Sample records for the tables that are NOT part of the campaign content:
-- the demo login account. The 25 campaign levels (topic + quizzes + 125
-- questions + 500 choices) live in database/campaign.sql and are inserted by
-- running that script right after this one:
--     sqlite3 codequest.db < database/schema.sql
--     sqlite3 codequest.db < database/seed.sql
--     sqlite3 codequest.db < database/campaign.sql
-- On an existing installation the startup migration (Data/DatabaseMigrator.cs)
-- ensures the same campaign rows without needing these files.
--
-- The password of the demo user is 'codequest123'; the table stores its
-- PBKDF2-HMAC-SHA256 hash only, in the format 'pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>'.
-- =============================================================================

PRAGMA foreign_keys = ON;


-- -----------------------------------------------------------------------------
-- users — exactly one row (plan assumption #1: a single local user).
-- password_hash = PBKDF2-HMAC-SHA256("codequest123", random 16-byte salt, 600,000 iterations)
-- -----------------------------------------------------------------------------
INSERT INTO users (user_id, username, password_hash, display_name, created_at) VALUES
    (1, 'demo', 'pbkdf2-sha256:600000:iCiatnQMd0AiHO3ww38HRg==:8vrsmmlXkmYgxd+YeaUNe70EGg0dG1QfxTggC/Unduo=',
        'Demo Student', '2026-11-01T09:00:00Z');
