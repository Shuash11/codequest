-- =============================================================================
-- CodeQuest — database/seed.sql
-- Sample records for every table (exam deliverable: "Sample records for
-- testing", docs/oop-final-exam-guidelines.md line 147).
--
-- Phase   : P1 — Database Schema, Seed Data & 3NF Documentation
-- Run after database/schema.sql on a fresh database:
--     sqlite3 codequest.db < database/schema.sql
--     sqlite3 codequest.db < database/seed.sql
--
-- What the sample data is for:
--   * 1 user          — the login form (P7) validates against this row.
--   * 5 topics        — programming (C#, Python, SQL) and cybersecurity
--                        (Networking, Web Security).
--   * 3 Approved quizzes, 5 questions each, 4 choices each, exactly one
--     correct choice per question — the takeable offline demo data (P9, P10).
--   * 2 Draft quizzes — the AI/review screen (P13) needs Draft data to demo.
--   * 4 attempts + 20 submitted answers — history (P10) and the report
--     screens (P11, P12) need real numbers to show.
--
-- All primary keys are inserted explicitly so that the script is deterministic
-- and so that the demo data can be referred to by a stable id.
-- ID map:
--   users              1
--   topics             1..5
--   quizzes            1..5      (1-3 Approved, 4-5 Draft)
--   questions          1..21     (quiz 1: 1-5, quiz 2: 6-10, quiz 3: 11-15,
--                                  quiz 4: 16-18, quiz 5: 19-21)
--   choices            1..84     (4 choices per question, in question order)
--   quiz_attempts      1..4
--   submitted_answers  20 rows   (5 answered questions per attempt)
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


-- -----------------------------------------------------------------------------
-- topics — five subjects: three programming, two cybersecurity.
-- -----------------------------------------------------------------------------
INSERT INTO topics (topic_id, name, category, description, created_at) VALUES
    (1, 'C#',            'Programming',   'C# fundamentals: types, classes, collections, and LINQ.',              '2026-11-01T09:00:00Z'),
    (2, 'Python',        'Programming',   'Python fundamentals: syntax, data structures, and functions.',         '2026-11-01T09:00:00Z'),
    (3, 'SQL',           'Programming',   'SQL and relational fundamentals: SELECT, JOIN, GROUP BY, aggregation.',  '2026-11-01T09:00:00Z'),
    (4, 'Networking',    'Cybersecurity', 'Networking fundamentals: TCP/IP, DHCP, DNS, and HTTP.',                  '2026-11-01T09:00:00Z'),
    (5, 'Web Security',  'Cybersecurity', 'Web application security: XSS, CSRF, injection, and security headers.',   '2026-11-01T09:00:00Z');


-- -----------------------------------------------------------------------------
-- quizzes — three takeable (Approved), two waiting for review (Draft).
-- A Draft quiz is listed in the UI as "pending review" and cannot be started.
-- -----------------------------------------------------------------------------
INSERT INTO quizzes (quiz_id, topic_id, title, difficulty, status, created_at) VALUES
    (1, 1, 'C# Core Concepts',         'Easy',   'Approved', '2026-11-01T09:10:00Z'),
    (2, 2, 'Python Essentials',         'Medium', 'Approved', '2026-11-01T09:20:00Z'),
    (3, 3, 'SQL Query Fundamentals',    'Easy',   'Approved', '2026-11-01T09:30:00Z'),
    (4, 4, 'Networking Basics',         'Medium', 'Draft',    '2026-11-01T09:40:00Z'),
    (5, 5, 'Web Security Spot Checks',  'Hard',   'Draft',    '2026-11-01T09:50:00Z');


-- =============================================================================
-- Quiz 1 — "C# Core Concepts" (topic C#, Easy, Approved)
-- 5 questions, 4 choices each, exactly one choice with is_correct = 1.
-- =============================================================================

-- Question 1 (question_id 1, choices 1-4, correct choice = 2)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (1, 1, 'Which C# keyword declares a constant whose value is fixed at compile time?',
        'const declares a compile-time constant. readonly allows the value to be assigned later in a constructor, while static and sealed say nothing about a fixed value.',
        1, '2026-11-01T09:10:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (1, 1, 'static',   0, 1),
    (2, 1, 'const',    1, 2),
    (3, 1, 'readonly', 0, 3),
    (4, 1, 'sealed',   0, 4);

-- Question 2 (question_id 2, choices 5-8, correct choice = 7)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (2, 1, 'Which C# collection guarantees unique keys and offers fast lookup by key?',
        'Dictionary<TKey, TValue> stores key/value pairs, rejects duplicate keys, and finds a value by key in constant time.',
        2, '2026-11-01T09:10:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (5, 2, 'List<T>',                   0, 1),
    (6, 2, 'Queue<T>',                  0, 2),
    (7, 2, 'Dictionary<TKey, TValue>',  1, 3),
    (8, 2, 'Array',                     0, 4);

-- Question 3 (question_id 3, choices 9-12, correct choice = 9)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (3, 1, 'What does the await keyword do inside an async method?',
        'await suspends the method and gives control back to the caller; the thread is released and the method continues when the awaited task completes.',
        3, '2026-11-01T09:10:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (9,  3, 'It pauses the method until the awaited task finishes, without blocking the thread', 1, 1),
    (10, 3, 'It blocks the thread until the awaited task finishes',                              0, 2),
    (11, 3, 'It runs the awaited task synchronously',                                           0, 3),
    (12, 3, 'It cancels the awaited task',                                                       0, 4);

-- Question 4 (question_id 4, choices 13-16, correct choice = 16)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (4, 1, 'Which LINQ operator keeps only the elements that match a condition?',
        'Where filters a sequence, Select projects it, OrderBy sorts it, and ToList materialises the result into a list.',
        4, '2026-11-01T09:10:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (13, 4, 'Select',  0, 1),
    (14, 4, 'OrderBy', 0, 2),
    (15, 4, 'ToList',  0, 3),
    (16, 4, 'Where',   1, 4);

-- Question 5 (question_id 5, choices 17-20, correct choice = 18)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (5, 1, 'Which interface lets a type define its own equality comparison against another type?',
        'IEquatable<T> defines a strongly typed Equals method; IDisposable handles cleanup, IEnumerable<T> is for iteration, and ICloneable is the legacy clone contract.',
        5, '2026-11-01T09:10:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (17, 5, 'IDisposable',    0, 1),
    (18, 5, 'IEquatable<T>',  1, 2),
    (19, 5, 'IEnumerable<T>', 0, 3),
    (20, 5, 'ICloneable',     0, 4);


-- =============================================================================
-- Quiz 2 — "Python Essentials" (topic Python, Medium, Approved)
-- 5 questions, 4 choices each, exactly one choice with is_correct = 1.
-- =============================================================================

-- Question 6 (question_id 6, choices 21-24, correct choice = 24)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (6, 2, 'Which Python type is an immutable, ordered sequence of items?',
        'A tuple is immutable and ordered. A list is mutable, a set is unordered, and a dict stores key/value pairs.',
        1, '2026-11-01T09:20:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (21, 6, 'list',  0, 1),
    (22, 6, 'set',   0, 2),
    (23, 6, 'dict',  0, 3),
    (24, 6, 'tuple', 1, 4);

-- Question 7 (question_id 7, choices 25-28, correct choice = 26)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (7, 2, 'What does the self parameter represent in a Python instance method?',
        'self is a reference to the instance the method was called on; the method works with the data of that object.',
        2, '2026-11-01T09:20:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (25, 7, 'The class in which the method was defined', 0, 1),
    (26, 7, 'The instance on which the method was called', 1, 2),
    (27, 7, 'The module that imported the method',     0, 3),
    (28, 7, 'The return value of the method',           0, 4);

-- Question 8 (question_id 8, choices 29-32, correct choice = 31)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (8, 2, 'Which keyword defines an anonymous function in Python?',
        'lambda creates a small anonymous function; def is the keyword used for a named function.',
        3, '2026-11-01T09:20:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (29, 8, 'def',    0, 1),
    (30, 8, 'func',   0, 2),
    (31, 8, 'lambda', 1, 3),
    (32, 8, 'anon',   0, 4);

-- Question 9 (question_id 9, choices 33-36, correct choice = 33)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (9, 2, 'Which method adds an item to the end of a Python list?',
        'list.append(item) adds an item at the end; insert() places an item at a given index instead.',
        4, '2026-11-01T09:20:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (33, 9, 'append',      1, 1),
    (34, 9, 'insert_last', 0, 2),
    (35, 9, 'push',        0, 3),
    (36, 9, 'add',         0, 4);

-- Question 10 (question_id 10, choices 37-40, correct choice = 39)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (10, 2, 'Which operator checks whether two names refer to the very same object?',
         'is compares object identity, while == compares the values the objects hold.',
         5, '2026-11-01T09:20:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (37, 10, '==',   0, 1),
    (38, 10, '===',  0, 2),
    (39, 10, 'is',   1, 3),
    (40, 10, 'eq',   0, 4);


-- =============================================================================
-- Quiz 3 — "SQL Query Fundamentals" (topic SQL, Easy, Approved)
-- 5 questions, 4 choices each, exactly one choice with is_correct = 1.
-- =============================================================================

-- Question 11 (question_id 11, choices 41-44, correct choice = 41)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (11, 3, 'Which SQL keyword retrieves rows from a table?',
         'SELECT reads rows; INSERT adds rows, UPDATE changes them, and DELETE removes them.',
         1, '2026-11-01T09:30:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (41, 11, 'SELECT', 1, 1),
    (42, 11, 'INSERT', 0, 2),
    (43, 11, 'UPDATE', 0, 3),
    (44, 11, 'DELETE', 0, 4);

-- Question 12 (question_id 12, choices 45-48, correct choice = 47)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (12, 3, 'Which clause filters the groups produced by GROUP BY?',
         'WHERE filters rows before the grouping; HAVING filters the grouped rows after the aggregation.',
         2, '2026-11-01T09:30:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (45, 12, 'WHERE',    0, 1),
    (46, 12, 'ORDER BY', 0, 2),
    (47, 12, 'HAVING',   1, 3),
    (48, 12, 'LIMIT',    0, 4);

-- Question 13 (question_id 13, choices 49-52, correct choice = 52)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (13, 3, 'Which join returns only the rows that have a match in both tables?',
         'INNER JOIN keeps only the matching rows, while the outer joins also keep unmatched rows from one side.',
         3, '2026-11-01T09:30:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (49, 13, 'LEFT JOIN',  0, 1),
    (50, 13, 'RIGHT JOIN', 0, 2),
    (51, 13, 'FULL JOIN',  0, 3),
    (52, 13, 'INNER JOIN', 1, 4);

-- Question 14 (question_id 14, choices 53-56, correct choice = 54)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (14, 3, 'Which aggregate function counts the non-NULL values of a column?',
         'COUNT(column) counts the non-NULL values of a column, while COUNT(*) counts the rows of the group.',
         4, '2026-11-01T09:30:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (53, 14, 'SUM',   0, 1),
    (54, 14, 'COUNT', 1, 2),
    (55, 14, 'AVG',   0, 3),
    (56, 14, 'MAX',   0, 4);

-- Question 15 (question_id 15, choices 57-60, correct choice = 59)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (15, 3, 'Which SQL statement removes rows from a table?',
         'DELETE removes rows from a table, while DROP removes the table itself.',
         5, '2026-11-01T09:30:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (57, 15, 'REMOVE', 0, 1),
    (58, 15, 'DROP',   0, 2),
    (59, 15, 'DELETE', 1, 3),
    (60, 15, 'CLEAR',  0, 4);


-- =============================================================================
-- Quiz 4 — "Networking Basics" (topic Networking, Medium, Draft)
-- A Draft quiz still holds questions and choices, but it is NOT takeable:
-- the review screen (P13) shows it as "pending review" until it is approved.
-- 3 questions, 4 choices each, exactly one choice with is_correct = 1.
-- =============================================================================

-- Question 16 (question_id 16, choices 61-64, correct choice = 62)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (16, 4, 'Which protocol assigns IP addresses to devices on a local network?',
         'DHCP hands out IP addresses to devices; DNS resolves names, HTTP carries web pages, and SMTP sends mail.',
         1, '2026-11-01T09:40:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (61, 16, 'DNS',  0, 1),
    (62, 16, 'DHCP', 1, 2),
    (63, 16, 'HTTP', 0, 3),
    (64, 16, 'SMTP', 0, 4);

-- Question 17 (question_id 17, choices 65-68, correct choice = 68)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (17, 4, 'What does DNS translate?',
         'DNS is the Domain Name System: it maps a human-readable host name to the IP address of a machine.',
         2, '2026-11-01T09:40:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (65, 17, 'MAC addresses into IP addresses', 0, 1),
    (66, 17, 'port numbers into service names', 0, 2),
    (67, 17, 'URLs into HTML pages',            0, 3),
    (68, 17, 'domain names into IP addresses',  1, 4);

-- Question 18 (question_id 18, choices 69-72, correct choice = 69)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (18, 4, 'Which transport-layer protocol guarantees ordered and reliable delivery?',
         'TCP adds sequencing, acknowledgements, and retransmission; UDP sends datagrams without those guarantees.',
         3, '2026-11-01T09:40:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (69, 18, 'TCP', 1, 1),
    (70, 18, 'UDP', 0, 2),
    (71, 18, 'ARP', 0, 3),
    (72, 18, 'IP',  0, 4);


-- =============================================================================
-- Quiz 5 — "Web Security Spot Checks" (topic Web Security, Hard, Draft)
-- The second Draft quiz, so the review screen has a cybersecurity example.
-- 3 questions, 4 choices each, exactly one choice with is_correct = 1.
-- =============================================================================

-- Question 19 (question_id 19, choices 73-76, correct choice = 75)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (19, 5, 'Which attack injects malicious script into a page that the victim''s browser then runs?',
         'XSS stores or reflects attacker script that runs in another user''s browser. SQL injection targets the database, and CSRF abuses the victim''s session.',
         1, '2026-11-01T09:50:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (73, 19, 'SQL injection',                      0, 1),
    (74, 19, 'Denial of service',                  0, 2),
    (75, 19, 'Cross-site scripting (XSS)',         1, 3),
    (76, 19, 'Cross-site request forgery (CSRF)',  0, 4);

-- Question 20 (question_id 20, choices 77-80, correct choice = 78)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (20, 5, 'Which attack makes an authenticated victim''s browser send an unintended request to a site the victim is logged in to?',
         'CSRF abuses the fact that the browser sends the credentials automatically and performs an action the user never intended.',
         2, '2026-11-01T09:50:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (77, 20, 'Cross-site scripting (XSS)',          0, 1),
    (78, 20, 'Cross-site request forgery (CSRF)',   1, 2),
    (79, 20, 'Server-side request forgery (SSRF)',  0, 3),
    (80, 20, 'XML external entity (XXE)',           0, 4);

-- Question 21 (question_id 21, choices 81-84, correct choice = 84)
INSERT INTO questions (question_id, quiz_id, question_text, explanation, position, created_at) VALUES
    (21, 5, 'Which HTTP response header tells the browser not to run scripts that arrive with the response?',
         'Content-Security-Policy restricts which scripts a page may run; the other headers cover framing, transport, and MIME sniffing.',
         3, '2026-11-01T09:50:00Z');
INSERT INTO choices (choice_id, question_id, choice_text, is_correct, position) VALUES
    (81, 21, 'X-Frame-Options',            0, 1),
    (82, 21, 'Strict-Transport-Security',  0, 2),
    (83, 21, 'X-Content-Type-Options',     0, 3),
    (84, 21, 'Content-Security-Policy',    1, 4);


-- =============================================================================
-- quiz_attempts — four completed runs by the demo user.
-- 'score' is the number of CORRECT answers. The number of answered questions
-- is the number of submitted_answers rows of that attempt (5 in every case),
-- so the percentage is always score / 5.
-- =============================================================================
INSERT INTO quiz_attempts (attempt_id, quiz_id, user_id, score, attempted_at) VALUES
    (1, 1, 1, 4, '2026-11-01T10:00:00Z'),
    (2, 1, 1, 2, '2026-11-01T11:00:00Z'),
    (3, 2, 1, 5, '2026-11-01T12:00:00Z'),
    (4, 3, 1, 3, '2026-11-01T13:00:00Z');


-- =============================================================================
-- submitted_answers — what was actually picked in each attempt.
-- Exactly one row per answered question; the composite foreign key makes sure
-- the chosen option really belongs to the answered question.
-- =============================================================================

-- Attempt 1 — quiz 1, 4 of 5 correct (question 4 was answered wrongly)
INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
    (1, 1, 2),
    (1, 2, 7),
    (1, 3, 9),
    (1, 4, 15),
    (1, 5, 18);

-- Attempt 2 — quiz 1, 2 of 5 correct (questions 1, 2 and 4 were answered wrongly)
INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
    (2, 1, 1),
    (2, 2, 6),
    (2, 3, 9),
    (2, 4, 13),
    (2, 5, 18);

-- Attempt 3 — quiz 2, 5 of 5 correct (a perfect run)
INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
    (3, 6, 24),
    (3, 7, 26),
    (3, 8, 31),
    (3, 9, 33),
    (3, 10, 39);

-- Attempt 4 — quiz 3, 3 of 5 correct (questions 12 and 14 were answered wrongly)
INSERT INTO submitted_answers (attempt_id, question_id, chosen_choice_id) VALUES
    (4, 11, 41),
    (4, 12, 45),
    (4, 13, 52),
    (4, 14, 53),
    (4, 15, 59);


-- =============================================================================
-- Expected report values for the sample data
-- (hand-computed reference for the report services of phase P11 and the
--  report screen of phase P12 — the unit tests should assert these numbers)
--
--   Total attempts ................ 4
--   Distinct quizzes taken ....... 3   (quizzes 1, 2 and 3)
--   Total answered questions ..... 20  (5 per attempt)
--   Total correct answers ........ 14  (4 + 2 + 5 + 3)
--   Overall average .............. 70 % (14 / 20)
--
--   Per topic
--     C# ............ 2 attempts, 6/10 correct, average 60 %, best 80 %, latest 40 %
--     Python ........ 1 attempt,  5/5  correct, average 100 %, best 100 %, latest 100 %
--     SQL ........... 1 attempt,  3/5  correct, average 60 %, best 60 %, latest 60 %
--     Networking .... 0 attempts (empty result, not an error)
--     Web Security .. 0 attempts (empty result, not an error)
--
--   Per difficulty
--     Easy ........... 3 attempts (quizzes 1 and 3), 9/15 correct, average 60 %
--     Medium ......... 1 attempt  (quiz 2), 5/5 correct, average 100 %
--     Hard ........... 0 attempts (the one Hard quiz is still Draft)
--
--   "Needs practice" list (average below 70 %): C# (60 %) and SQL (60 %).
-- =============================================================================
