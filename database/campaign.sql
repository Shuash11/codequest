-- =============================================================================
-- CodeQuest — database/campaign.sql
-- The 25 built-in campaign levels (task 5): one Campaign topic, the approved
-- quizzes "Level 1".."Level 25" with 5 four-option questions each (exactly one
-- correct answer per question) and difficulty by the ladder —
-- L1-5 Easy, L6-12 Medium, L13-19 Hard, L20-25 Insane.
--
-- Run after database/schema.sql and database/seed.sql:
--     sqlite3 codequest.db < database/schema.sql
--     sqlite3 codequest.db < database/seed.sql
--     sqlite3 codequest.db < database/campaign.sql
--
-- The same script is embedded in the app and replayed by the startup migration
-- (Data/DatabaseMigrator.cs), so EVERY statement guards itself with a
-- WHERE NOT EXISTS clause; each level also repairs a wrongly seeded row of
-- the Campaign topic (wrong difficulty or a Draft status) to the ladder
-- values, and a re-run changes nothing.
-- =============================================================================

PRAGMA foreign_keys = ON;


-- The single campaign topic that owns every level.
INSERT INTO topics (name, category, description, created_at)
SELECT 'Campaign', 'Programming', 'The progressive campaign — clear every level.', '2026-11-12T08:59:00Z'
WHERE NOT EXISTS (SELECT 1 FROM topics WHERE name = 'Campaign');

-- ----------------------------------------------------------------------------
-- Level 1 (Easy): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 1', 'Easy', 'Approved', '2026-11-12T09:00:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 1'
  );
UPDATE quizzes
SET difficulty = 'Easy', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 1'
  AND (difficulty <> 'Easy' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Easy'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 1'
  )
  AND difficulty <> 'Easy';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 1' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which C# keyword declares a value that is fixed at compile time?', 'const fixes the value at compile time; readonly may still be assigned in a constructor.', 1),
        ('Which C# type stores a true or false value?', 'bool holds exactly two values: true and false.', 2),
        ('Which operator gives the remainder of an integer division?', 'The % operator returns the remainder; / returns the integer quotient.', 3),
        ('Which collection serves items first in, first out?', 'A Queue<T> dequeues in the same order it enqueues; a Stack<T> is last in, first out.', 4),
        ('Which character joins two strings with the concatenation operator?', 'The + operator concatenates strings, for example "a" + "b".', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Easy', q.position, '2026-11-12T09:00:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 1' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'static', 0, 1),
        (1, 'const', 1, 2),
        (1, 'readonly', 0, 3),
        (1, 'sealed', 0, 4),
        (2, 'bool', 1, 1),
        (2, 'bit', 0, 2),
        (2, 'int', 0, 3),
        (2, 'char', 0, 4),
        (3, '%', 1, 1),
        (3, '/', 0, 2),
        (3, '*', 0, 3),
        (3, '^', 0, 4),
        (4, 'Stack<T>', 0, 1),
        (4, 'Queue<T>', 1, 2),
        (4, 'List<T>', 0, 3),
        (4, 'Dictionary<TKey, TValue>', 0, 4),
        (5, '&', 0, 1),
        (5, '*', 0, 2),
        (5, '+', 1, 3),
        (5, '#', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 2 (Easy): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 2', 'Easy', 'Approved', '2026-11-12T09:01:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 2'
  );
UPDATE quizzes
SET difficulty = 'Easy', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 2'
  AND (difficulty <> 'Easy' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Easy'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 2'
  )
  AND difficulty <> 'Easy';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 2' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which keyword creates a new object?', 'new allocates the object and runs its constructor.', 1),
        ('Which loop always runs its body at least once?', 'A do-while checks its condition after the body, so the first pass always runs.', 2),
        ('Which method turns the text "42" into the number 42?', 'int.Parse reads a text value and returns the matching number.', 3),
        ('Which type stores a 64-bit signed integer?', 'long is a 64-bit signed integer; int is 32 bits.', 4),
        ('Which symbol ends a C# statement?', 'Every C# statement ends with a semicolon.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Easy', q.position, '2026-11-12T09:01:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 2' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'new', 1, 1),
        (1, 'make', 0, 2),
        (1, 'obj', 0, 3),
        (1, 'init', 0, 4),
        (2, 'while', 0, 1),
        (2, 'do-while', 1, 2),
        (2, 'for', 0, 3),
        (2, 'foreach', 0, 4),
        (3, 'int.ToString', 0, 1),
        (3, 'int.Parse', 1, 2),
        (3, 'int.Format', 0, 3),
        (3, 'int.Cast', 0, 4),
        (4, 'int', 0, 1),
        (4, 'short', 0, 2),
        (4, 'long', 1, 3),
        (4, 'byte', 0, 4),
        (5, '.', 0, 1),
        (5, ';', 1, 2),
        (5, ':', 0, 3),
        (5, '~', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 3 (Easy): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 3', 'Easy', 'Approved', '2026-11-12T09:02:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 3'
  );
UPDATE quizzes
SET difficulty = 'Easy', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 3'
  AND (difficulty <> 'Easy' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Easy'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 3'
  )
  AND difficulty <> 'Easy';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 3' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which access modifier makes a member visible only inside its own class?', 'private limits the member to the declaring class; internal opens it to the whole assembly.', 1),
        ('Which keyword exits a loop immediately?', 'break leaves the loop at once; continue only skips the rest of the current pass.', 2),
        ('Which C# structure maps keys to values?', 'A dictionary stores key and value pairs and looks values up by key.', 3),
        ('Which prefix starts a string interpolation expression?', 'The $ prefix enables interpolation, for example $"Level {n}".', 4),
        ('Which type represents a sequence of characters?', 'string is an immutable sequence of char values.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Easy', q.position, '2026-11-12T09:02:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 3' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'public', 0, 1),
        (1, 'protected', 0, 2),
        (1, 'private', 1, 3),
        (1, 'internal', 0, 4),
        (2, 'continue', 0, 1),
        (2, 'break', 1, 2),
        (2, 'return', 0, 3),
        (2, 'stop', 0, 4),
        (3, 'list', 0, 1),
        (3, 'array', 0, 2),
        (3, 'queue', 0, 3),
        (3, 'dictionary', 1, 4),
        (4, '$', 1, 1),
        (4, '@', 0, 2),
        (4, '#', 0, 3),
        (4, '~', 0, 4),
        (5, 'char', 0, 1),
        (5, 'text', 0, 2),
        (5, 'string', 1, 3),
        (5, 'byte', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 4 (Easy): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 4', 'Easy', 'Approved', '2026-11-12T09:03:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 4'
  );
UPDATE quizzes
SET difficulty = 'Easy', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 4'
  AND (difficulty <> 'Easy' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Easy'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 4'
  )
  AND difficulty <> 'Easy';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 4' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which property gives the number of characters in a string?', 'string.Length returns the number of chars in the string.', 1),
        ('Which keyword declares a variable whose type the compiler infers?', 'var asks the compiler to infer the type from the assigned value.', 2),
        ('What is the result of 7 / 2 when both values are int?', 'Integer division truncates: 7 / 2 is 3; 7 % 2 is 1.', 3),
        ('Which keyword starts a conditional branch?', 'if evaluates a condition and runs its block when the condition is true.', 4),
        ('Which operator checks two values for equality?', '== compares values; = assigns.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Easy', q.position, '2026-11-12T09:03:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 4' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'Size', 0, 1),
        (1, 'Length', 1, 2),
        (1, 'Count', 0, 3),
        (1, 'Capacity', 0, 4),
        (2, 'let', 0, 1),
        (2, 'auto', 0, 2),
        (2, 'var', 1, 3),
        (2, 'dynamic', 0, 4),
        (3, '3.5', 0, 1),
        (3, '4', 0, 2),
        (3, '3', 1, 3),
        (3, '2', 0, 4),
        (4, 'if', 1, 1),
        (4, 'when', 0, 2),
        (4, 'unless', 0, 3),
        (4, 'check', 0, 4),
        (5, '=', 0, 1),
        (5, '!=', 0, 2),
        (5, '==', 1, 3),
        (5, '===', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 5 (Easy): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 5', 'Easy', 'Approved', '2026-11-12T09:04:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 5'
  );
UPDATE quizzes
SET difficulty = 'Easy', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 5'
  AND (difficulty <> 'Easy' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Easy'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 5'
  )
  AND difficulty <> 'Easy';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 5' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which index holds the first element of a C# array?', 'Arrays are zero-based, so the first element sits at index 0.', 1),
        ('Which keyword marks a method that returns no value?', 'void means the method performs work but returns nothing.', 2),
        ('Which collection keeps items in order and allows duplicates?', 'A List<T> keeps insertion order and accepts duplicates; sets reject duplicates.', 3),
        ('Which operator means both conditions must be true?', '&& is the logical AND; || is the logical OR.', 4),
        ('Which namespace contains the Console class?', 'Console lives directly in the System namespace.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Easy', q.position, '2026-11-12T09:04:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 5' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, '1', 0, 1),
        (1, '0', 1, 2),
        (1, '-1', 0, 3),
        (1, 'first', 0, 4),
        (2, 'null', 0, 1),
        (2, 'void', 1, 2),
        (2, 'empty', 0, 3),
        (2, 'none', 0, 4),
        (3, 'HashSet<T>', 0, 1),
        (3, 'List<T>', 1, 2),
        (3, 'Dictionary<TKey, TValue>', 0, 3),
        (3, 'SortedSet<T>', 0, 4),
        (4, '||', 0, 1),
        (4, '&&', 1, 2),
        (4, '!', 0, 3),
        (4, '^', 0, 4),
        (5, 'System.Text', 0, 1),
        (5, 'System.IO', 0, 2),
        (5, 'System', 1, 3),
        (5, 'System.Data', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 6 (Medium): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 6', 'Medium', 'Approved', '2026-11-12T09:05:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 6'
  );
UPDATE quizzes
SET difficulty = 'Medium', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 6'
  AND (difficulty <> 'Medium' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Medium'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 6'
  )
  AND difficulty <> 'Medium';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 6' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which OOP principle hides internal state behind a public surface?', 'Encapsulation keeps fields private and exposes behavior through public members.', 1),
        ('Which keyword on a base method allows a subclass to replace its behavior?', 'A virtual method can be overridden; sealed methods cannot.', 2),
        ('Which keyword prevents a class from being inherited?', 'A sealed class cannot have subclasses.', 3),
        ('Which member declares a method every subclass must implement?', 'An abstract member has no body and forces every concrete subclass to implement it.', 4),
        ('Which keyword refers to the current object?', 'this refers to the instance the method runs on; base refers to the parent class.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Medium', q.position, '2026-11-12T09:05:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 6' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'Encapsulation', 1, 1),
        (1, 'Inheritance', 0, 2),
        (1, 'Overloading', 0, 3),
        (1, 'Compilation', 0, 4),
        (2, 'sealed', 0, 1),
        (2, 'virtual', 1, 2),
        (2, 'static', 0, 3),
        (2, 'readonly', 0, 4),
        (3, 'static', 0, 1),
        (3, 'sealed', 1, 2),
        (3, 'final', 0, 3),
        (3, 'locked', 0, 4),
        (4, 'virtual', 0, 1),
        (4, 'private', 0, 2),
        (4, 'abstract', 1, 3),
        (4, 'partial', 0, 4),
        (5, 'self', 0, 1),
        (5, 'base', 0, 2),
        (5, 'this', 1, 3),
        (5, 'me', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 7 (Medium): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 7', 'Medium', 'Approved', '2026-11-12T09:06:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 7'
  );
UPDATE quizzes
SET difficulty = 'Medium', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 7'
  AND (difficulty <> 'Medium' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Medium'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 7'
  )
  AND difficulty <> 'Medium';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 7' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('A base reference that calls a derived override is which principle?', 'Polymorphism lets one base reference run the override of the actual object type.', 1),
        ('Which keyword calls a constructor of the base class?', 'base(...) in a constructor initializer runs the matching base constructor.', 2),
        ('How many base classes can one C# class have?', 'C# allows single inheritance of classes; interfaces can be implemented in any number.', 3),
        ('Which keyword defines a contract with no implementation?', 'An interface lists member signatures that implementing types must provide.', 4),
        ('What does an object print by default through ToString()?', 'The default ToString() returns the full name of the object type.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Medium', q.position, '2026-11-12T09:06:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 7' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'Polymorphism', 1, 1),
        (1, 'Encapsulation', 0, 2),
        (1, 'Composition', 0, 3),
        (1, 'Compilation', 0, 4),
        (2, 'super', 0, 1),
        (2, 'base', 1, 2),
        (2, 'parent', 0, 3),
        (2, 'this', 0, 4),
        (3, 'two', 0, 1),
        (3, 'unlimited', 0, 2),
        (3, 'one', 1, 3),
        (3, 'zero', 0, 4),
        (4, 'class', 0, 1),
        (4, 'struct', 0, 2),
        (4, 'interface', 1, 3),
        (4, 'enum', 0, 4),
        (5, 'its fields', 0, 1),
        (5, 'its hash code', 0, 2),
        (5, 'its full type name', 1, 3),
        (5, 'nothing', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 8 (Medium): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 8', 'Medium', 'Approved', '2026-11-12T09:07:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 8'
  );
UPDATE quizzes
SET difficulty = 'Medium', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 8'
  AND (difficulty <> 'Medium' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Medium'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 8'
  )
  AND difficulty <> 'Medium';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 8' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which LINQ operator transforms every element?', 'Select projects each element into a new shape.', 1),
        ('Which LINQ operator keeps the elements that match a condition?', 'Where filters a sequence with a predicate.', 2),
        ('Which method runs a query and returns a List<T>?', 'ToList materializes the sequence into a List<T>.', 3),
        ('Which operator groups elements by a key?', 'GroupBy builds one group per distinct key.', 4),
        ('Which method returns the first element or a default value?', 'FirstOrDefault returns default(T) when the sequence is empty instead of throwing.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Medium', q.position, '2026-11-12T09:07:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 8' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'Where', 0, 1),
        (1, 'Select', 1, 2),
        (1, 'Find', 0, 3),
        (1, 'Filter', 0, 4),
        (2, 'Filter', 0, 1),
        (2, 'Select', 0, 2),
        (2, 'Where', 1, 3),
        (2, 'Any', 0, 4),
        (3, 'ToArray', 0, 1),
        (3, 'ToList', 1, 2),
        (3, 'First', 0, 3),
        (3, 'Count', 0, 4),
        (4, 'OrderBy', 0, 1),
        (4, 'Join', 0, 2),
        (4, 'GroupBy', 1, 3),
        (4, 'Split', 0, 4),
        (5, 'First', 0, 1),
        (5, 'Single', 0, 2),
        (5, 'Any', 0, 3),
        (5, 'FirstOrDefault', 1, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 9 (Medium): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 9', 'Medium', 'Approved', '2026-11-12T09:08:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 9'
  );
UPDATE quizzes
SET difficulty = 'Medium', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 9'
  AND (difficulty <> 'Medium' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Medium'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 9'
  )
  AND difficulty <> 'Medium';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 9' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which operator sorts elements ascending?', 'OrderBy sorts ascending; OrderByDescending sorts the other way.', 1),
        ('Which operator counts the elements of a sequence?', 'Count returns the number of elements.', 2),
        ('Which method joins two sequences by a key?', 'Join pairs elements of two sequences that share a key.', 3),
        ('Which operator flattens child collections into one sequence?', 'SelectMany projects each element to a sequence and flattens the results.', 4),
        ('Which method returns true when at least one element matches?', 'Any returns true as soon as one element satisfies the predicate.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Medium', q.position, '2026-11-12T09:08:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 9' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'Sort', 0, 1),
        (1, 'OrderBy', 1, 2),
        (1, 'Arrange', 0, 3),
        (1, 'Rank', 0, 4),
        (2, 'Sum', 0, 1),
        (2, 'Total', 0, 2),
        (2, 'Count', 1, 3),
        (2, 'Length', 0, 4),
        (3, 'Merge', 0, 1),
        (3, 'Join', 1, 2),
        (3, 'Link', 0, 3),
        (3, 'Attach', 0, 4),
        (4, 'Select', 0, 1),
        (4, 'Flatten', 0, 2),
        (4, 'SelectMany', 1, 3),
        (4, 'Merge', 0, 4),
        (5, 'All', 0, 1),
        (5, 'Any', 1, 2),
        (5, 'ContainsAll', 0, 3),
        (5, 'Exists', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 10 (Medium): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 10', 'Medium', 'Approved', '2026-11-12T09:09:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 10'
  );
UPDATE quizzes
SET difficulty = 'Medium', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 10'
  AND (difficulty <> 'Medium' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Medium'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 10'
  )
  AND difficulty <> 'Medium';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 10' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which syntax declares a generic method?', 'The type parameter sits in angle brackets after the method name.', 1),
        ('Which generic constraint requires a public parameterless constructor?', 'new() guarantees the type can be created with new T().', 2),
        ('What is T in List<T> called?', 'T is the type parameter; the concrete type passed in is the type argument.', 3),
        ('Which constraint limits T to reference types?', 'The class constraint allows reference types only.', 4),
        ('Which collection is type-safe and resizable?', 'List<T> grows dynamically and keeps every element type-safe.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Medium', q.position, '2026-11-12T09:09:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 10' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, '<T>', 1, 1),
        (1, '[T]', 0, 2),
        (1, '(T)', 0, 3),
        (1, '{T}', 0, 4),
        (2, 'class', 0, 1),
        (2, 'new()', 1, 2),
        (2, 'struct', 0, 3),
        (2, 'notnull', 0, 4),
        (3, 'type argument', 0, 1),
        (3, 'template key', 0, 2),
        (3, 'type parameter', 1, 3),
        (3, 'generic value', 0, 4),
        (4, 'struct', 0, 1),
        (4, 'class', 1, 2),
        (4, 'new()', 0, 3),
        (4, 'enum', 0, 4),
        (5, 'Array', 0, 1),
        (5, 'ArrayList', 0, 2),
        (5, 'List<T>', 1, 3),
        (5, 'object[]', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 11 (Medium): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 11', 'Medium', 'Approved', '2026-11-12T09:10:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 11'
  );
UPDATE quizzes
SET difficulty = 'Medium', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 11'
  AND (difficulty <> 'Medium' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Medium'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 11'
  )
  AND difficulty <> 'Medium';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 11' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which block always runs after try and catch?', 'finally runs even when the try block throws or returns.', 1),
        ('Which keyword raises an exception?', 'throw starts the exception; catch handles it.', 2),
        ('Which type must every custom exception derive from?', 'Custom exceptions derive from System.Exception.', 3),
        ('Which keyword catches one specific exception type?', 'catch names the exception type it can handle.', 4),
        ('What should code do with an exception it cannot handle?', 'Letting it propagate keeps the failure visible instead of hiding it.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Medium', q.position, '2026-11-12T09:10:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 11' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'final', 0, 1),
        (1, 'done', 0, 2),
        (1, 'finally', 1, 3),
        (1, 'finish', 0, 4),
        (2, 'raise', 0, 1),
        (2, 'throw', 1, 2),
        (2, 'error', 0, 3),
        (2, 'fail', 0, 4),
        (3, 'Exception', 1, 1),
        (3, 'Error', 0, 2),
        (3, 'Throwable', 0, 3),
        (3, 'Fault', 0, 4),
        (4, 'handle', 0, 1),
        (4, 'catch', 1, 2),
        (4, 'except', 0, 3),
        (4, 'rescue', 0, 4),
        (5, 'swallow it', 0, 1),
        (5, 'log and continue', 0, 2),
        (5, 'ignore it', 0, 3),
        (5, 'let it propagate', 1, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 12 (Medium): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 12', 'Medium', 'Approved', '2026-11-12T09:11:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 12'
  );
UPDATE quizzes
SET difficulty = 'Medium', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 12'
  AND (difficulty <> 'Medium' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Medium'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 12'
  )
  AND difficulty <> 'Medium';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 12' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which keyword marks a method that can await?', 'async enables await inside the method body.', 1),
        ('Which keyword pauses a method without blocking the thread?', 'await releases the thread until the awaited task finishes.', 2),
        ('What does a Task represent?', 'A Task models work that completes in the future, possibly on another thread.', 3),
        ('Which awaitable method completes when every task in a collection completes?', 'Task.WhenAll returns a task that finishes when every input task has finished.', 4),
        ('Which suffix conventionally ends an async method name?', 'The Async suffix, for example LoadAsync, marks an awaitable method.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Medium', q.position, '2026-11-12T09:11:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 12' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'async', 1, 1),
        (1, 'await', 0, 2),
        (1, 'task', 0, 3),
        (1, 'defer', 0, 4),
        (2, 'wait', 0, 1),
        (2, 'yield', 0, 2),
        (2, 'await', 1, 3),
        (2, 'sleep', 0, 4),
        (3, 'a completed value', 0, 1),
        (3, 'an operation that finishes later', 1, 2),
        (3, 'a thread', 0, 3),
        (3, 'a lock', 0, 4),
        (4, 'Task.WhenAll', 1, 1),
        (4, 'Task.WaitAll', 0, 2),
        (4, 'Task.Run', 0, 3),
        (4, 'Task.Delay', 0, 4),
        (5, 'Task', 0, 1),
        (5, 'Await', 0, 2),
        (5, 'Async', 1, 3),
        (5, 'Sync', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 13 (Hard): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 13', 'Hard', 'Approved', '2026-11-12T09:12:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 13'
  );
UPDATE quizzes
SET difficulty = 'Hard', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 13'
  AND (difficulty <> 'Hard' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Hard'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 13'
  )
  AND difficulty <> 'Hard';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 13' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('What does a delegate hold?', 'A delegate is a type-safe reference to a method with a matching signature.', 1),
        ('Which operator subscribes a handler to an event?', 'The += operator adds a handler to the invocation list; -= removes it.', 2),
        ('What happens when a multicast delegate is invoked?', 'A multicast delegate calls every method in its invocation list, in order.', 3),
        ('Which member lets other objects subscribe to notifications?', 'An event exposes add and remove access instead of the whole delegate.', 4),
        ('Which delegate type returns a value and takes no parameters?', 'Func<T> takes no parameters and returns T; Action returns void.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Hard', q.position, '2026-11-12T09:12:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 13' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'a copy of an object', 0, 1),
        (1, 'a reference to a method', 1, 2),
        (1, 'a value type', 0, 3),
        (1, 'a thread id', 0, 4),
        (2, '==', 0, 1),
        (2, '+=', 1, 2),
        (2, '-=', 0, 3),
        (2, '=>', 0, 4),
        (3, 'only the last method runs', 0, 1),
        (3, 'it throws', 0, 2),
        (3, 'all methods in its invocation list run', 1, 3),
        (3, 'it runs nothing', 0, 4),
        (4, 'field', 0, 1),
        (4, 'event', 1, 2),
        (4, 'property', 0, 3),
        (4, 'indexer', 0, 4),
        (5, 'Action', 0, 1),
        (5, 'Predicate<T>', 0, 2),
        (5, 'Func<T>', 1, 3),
        (5, 'EventHandler', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 14 (Hard): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 14', 'Hard', 'Approved', '2026-11-12T09:13:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 14'
  );
UPDATE quizzes
SET difficulty = 'Hard', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 14'
  AND (difficulty <> 'Hard' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Hard'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 14'
  )
  AND difficulty <> 'Hard';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 14' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which class is the entry point for runtime type information?', 'object.GetType() returns a Type object that describes the runtime type.', 1),
        ('Which namespace contains the reflection APIs?', 'System.Reflection holds Type, MethodInfo, PropertyInfo and the rest.', 2),
        ('Which interface releases unmanaged resources deterministically?', 'Dispose releases resources at a known point; a finalizer runs at an unknown one.', 3),
        ('Which statement guarantees Dispose is called even when an exception is thrown?', 'using compiles to try/finally, so Dispose runs on every exit path.', 4),
        ('When does the garbage collector free an unreachable object?', 'GC timing is nondeterministic; code must not depend on when it runs.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Hard', q.position, '2026-11-12T09:13:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 14' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'Object', 0, 1),
        (1, 'Member', 0, 2),
        (1, 'Type', 1, 3),
        (1, 'Assembly', 0, 4),
        (2, 'System.Reflection', 1, 1),
        (2, 'System.Runtime', 0, 2),
        (2, 'System.Meta', 0, 3),
        (2, 'System.Types', 0, 4),
        (3, 'IClosable', 0, 1),
        (3, 'IDisposable', 1, 2),
        (3, 'IFinalizable', 0, 3),
        (3, 'IDestructible', 0, 4),
        (4, 'final', 0, 1),
        (4, 'lock', 0, 2),
        (4, 'using', 1, 3),
        (4, 'fixed', 0, 4),
        (5, 'immediately', 0, 1),
        (5, 'never', 0, 2),
        (5, 'at a time it chooses', 1, 3),
        (5, 'only at shutdown', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 15 (Hard): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 15', 'Hard', 'Approved', '2026-11-12T09:14:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 15'
  );
UPDATE quizzes
SET difficulty = 'Hard', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 15'
  AND (difficulty <> 'Hard' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Hard'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 15'
  )
  AND difficulty <> 'Hard';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 15' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('What is converting an int into an object called?', 'Boxing wraps a value type on the heap; unboxing copies it back out.', 1),
        ('Which keyword guards a critical section?', 'lock takes a monitor so only one thread runs the block at a time.', 2),
        ('Which class increments a counter atomically?', 'Interlocked.Increment performs the read-modify-write as one atomic step.', 3),
        ('Which collection is built for concurrent dictionary access?', 'ConcurrentDictionary is safe for concurrent readers and writers.', 4),
        ('Which method returns the canonical interned string for a value?', 'String.Intern returns the shared instance from the intern pool.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Hard', q.position, '2026-11-12T09:14:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 15' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'unboxing', 0, 1),
        (1, 'casting', 0, 2),
        (1, 'boxing', 1, 3),
        (1, 'wrapping', 0, 4),
        (2, 'using', 0, 1),
        (2, 'lock', 1, 2),
        (2, 'fixed', 0, 3),
        (2, 'checked', 0, 4),
        (3, 'Math', 0, 1),
        (3, 'Interlocked', 1, 2),
        (3, 'Thread', 0, 3),
        (3, 'Monitor', 0, 4),
        (4, 'Dictionary<TKey, TValue>', 0, 1),
        (4, 'SortedDictionary<TKey, TValue>', 0, 2),
        (4, 'Hashtable', 0, 3),
        (4, 'ConcurrentDictionary<TKey, TValue>', 1, 4),
        (5, 'String.Copy', 0, 1),
        (5, 'String.Format', 0, 2),
        (5, 'String.Intern', 1, 3),
        (5, 'String.Concat', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 16 (Hard): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 16', 'Hard', 'Approved', '2026-11-12T09:15:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 16'
  );
UPDATE quizzes
SET difficulty = 'Hard', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 16'
  AND (difficulty <> 'Hard' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Hard'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 16'
  )
  AND difficulty <> 'Hard';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 16' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which type is a value type?', 'A struct is copied on assignment and lives on the stack or inline.', 1),
        ('What happens when a struct is assigned to another variable?', 'Struct assignment copies the whole value; the two variables are independent.', 2),
        ('When does a deferred LINQ query execute?', 'Deferred operators build a pipeline that runs when the sequence is enumerated.', 3),
        ('Which equality does == use for two strings?', 'string equality compares the characters, not the object identity.', 4),
        ('Which method forces a LINQ query to run immediately?', 'ToList enumerates the pipeline right away and stores the results.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Hard', q.position, '2026-11-12T09:15:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 16' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'class', 0, 1),
        (1, 'struct', 1, 2),
        (1, 'interface', 0, 3),
        (1, 'string', 0, 4),
        (2, 'both names share one instance', 0, 1),
        (2, 'it throws', 0, 2),
        (2, 'a copy is made', 1, 3),
        (2, 'it boxes', 0, 4),
        (3, 'at definition', 0, 1),
        (3, 'never', 0, 2),
        (3, 'when it is iterated', 1, 3),
        (3, 'when the process exits', 0, 4),
        (4, 'reference', 0, 1),
        (4, 'hash', 0, 2),
        (4, 'value', 1, 3),
        (4, 'type', 0, 4),
        (5, 'Where', 0, 1),
        (5, 'Select', 0, 2),
        (5, 'ToList', 1, 3),
        (5, 'OrderBy', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 17 (Hard): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 17', 'Hard', 'Approved', '2026-11-12T09:16:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 17'
  );
UPDATE quizzes
SET difficulty = 'Hard', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 17'
  AND (difficulty <> 'Hard' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Hard'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 17'
  )
  AND difficulty <> 'Hard';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 17' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which operation is one-way by design?', 'A hash cannot be reversed into the original data.', 1),
        ('Which algorithm is designed for password hashing?', 'bcrypt is deliberately slow and salted; AES and RSA are encryption algorithms.', 2),
        ('Which operation can be reversed with the right key?', 'Encryption is reversible with the key; hashing is not reversible at all.', 3),
        ('What does a per-user salt prevent?', 'The salt makes precomputed hash tables useless because equal passwords hash differently.', 4),
        ('Which transform adds no secrecy by itself?', 'Base64 is a reversible encoding, not a protection.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Hard', q.position, '2026-11-12T09:16:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 17' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'encryption', 0, 1),
        (1, 'encoding', 0, 2),
        (1, 'hashing', 1, 3),
        (1, 'compression', 0, 4),
        (2, 'AES', 0, 1),
        (2, 'RSA', 0, 2),
        (2, 'bcrypt', 1, 3),
        (2, 'Base64', 0, 4),
        (3, 'hashing', 0, 1),
        (3, 'salting', 0, 2),
        (3, 'encryption', 1, 3),
        (3, 'checksumming', 0, 4),
        (4, 'key theft', 0, 1),
        (4, 'rainbow table attacks', 1, 2),
        (4, 'packet sniffing', 0, 3),
        (4, 'denial of service', 0, 4),
        (5, 'encryption', 0, 1),
        (5, 'hashing', 0, 2),
        (5, 'Base64 encoding', 1, 3),
        (5, 'key wrapping', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 18 (Hard): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 18', 'Hard', 'Approved', '2026-11-12T09:17:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 18'
  );
UPDATE quizzes
SET difficulty = 'Hard', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 18'
  AND (difficulty <> 'Hard' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Hard'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 18'
  )
  AND difficulty <> 'Hard';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 18' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which defense stops SQL injection?', 'Parameters travel separately from the SQL text, so input can never change the statement.', 1),
        ('Which TLS element proves the server identity?', 'The certificate binds the server name to a public key the client can verify.', 2),
        ('What does the principle of least privilege grant?', 'Everyone gets only the permissions their task actually requires.', 3),
        ('Which port does HTTPS use by default?', 'HTTPS defaults to port 443; HTTP uses 80.', 4),
        ('Which practice makes injected markup harmless in output?', 'Output encoding turns markup characters into inert text.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Hard', q.position, '2026-11-12T09:17:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 18' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'string concatenation', 0, 1),
        (1, 'longer passwords', 0, 2),
        (1, 'parameterized queries', 1, 3),
        (1, 'caching', 0, 4),
        (2, 'the port', 0, 1),
        (2, 'the certificate', 1, 2),
        (2, 'the cipher list', 0, 3),
        (2, 'the session id', 0, 4),
        (3, 'all rights', 0, 1),
        (3, 'temporary admin rights', 0, 2),
        (3, 'the minimum access needed', 1, 3),
        (3, 'no authentication', 0, 4),
        (4, '80', 0, 1),
        (4, '22', 0, 2),
        (4, '443', 1, 3),
        (4, '8080', 0, 4),
        (5, 'minification', 0, 1),
        (5, 'output encoding', 1, 2),
        (5, 'caching', 0, 3),
        (5, 'gzip', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 19 (Hard): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 19', 'Hard', 'Approved', '2026-11-12T09:18:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 19'
  );
UPDATE quizzes
SET difficulty = 'Hard', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 19'
  AND (difficulty <> 'Hard' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Hard'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 19'
  )
  AND difficulty <> 'Hard';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 19' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which validation style is preferred for untrusted input?', 'An allowlist accepts only known-good input; a blocklist always misses something.', 1),
        ('What should a secure system do with actions that are not explicitly allowed?', 'Deny by default ensures new code paths stay closed until opened deliberately.', 2),
        ('What is defense in depth?', 'Independent layers mean one failed control does not expose the system.', 3),
        ('Where should application secrets live?', 'A secret manager stores and rotates secrets outside the code and its history.', 4),
        ('Which default is secure for a new service account?', 'New accounts start with the minimum rights and gain more only when needed.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Hard', q.position, '2026-11-12T09:18:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 19' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'blocklist', 0, 1),
        (1, 'guess list', 0, 2),
        (1, 'allowlist', 1, 3),
        (1, 'no validation', 0, 4),
        (2, 'allow them', 0, 1),
        (2, 'deny them', 1, 2),
        (2, 'log and allow', 0, 3),
        (2, 'retry them', 0, 4),
        (3, 'one strong control', 0, 1),
        (3, 'several independent controls', 1, 2),
        (3, 'hiding the server', 0, 3),
        (3, 'obfuscation', 0, 4),
        (4, 'in source control', 0, 1),
        (4, 'in client logs', 0, 2),
        (4, 'in a secret manager', 1, 3),
        (4, 'in comments', 0, 4),
        (5, 'full admin', 0, 1),
        (5, 'shared credentials', 0, 2),
        (5, 'least privilege', 1, 3),
        (5, 'no password policy', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 20 (Insane): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 20', 'Insane', 'Approved', '2026-11-12T09:19:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 20'
  );
UPDATE quizzes
SET difficulty = 'Insane', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 20'
  AND (difficulty <> 'Insane' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Insane'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 20'
  )
  AND difficulty <> 'Insane';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 20' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('What does Span<T> provide over a memory region?', 'Span<T> is a ref struct view that reads and writes memory without allocating.', 1),
        ('Why can Span<T> not be a field of a class?', 'A ref struct may live only on the stack, so a class field cannot hold one.', 2),
        ('Which type represents code that can be inspected as data?', 'An expression tree exposes the lambda body as a tree of nodes at runtime.', 3),
        ('Which call turns an expression tree into a runnable delegate?', 'Compile emits a delegate from the expression tree.', 4),
        ('Which allocation do stackalloc and Span<T> help you avoid?', 'Stack-only buffers skip the garbage collector completely.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Insane', q.position, '2026-11-12T09:19:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 20' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'a heap copy of the data', 0, 1),
        (1, 'a view over contiguous memory', 1, 2),
        (1, 'a thread-local buffer', 0, 3),
        (1, 'a locked array', 0, 4),
        (2, 'it is sealed', 0, 1),
        (2, 'it is generic', 0, 2),
        (2, 'it is a ref struct', 1, 3),
        (2, 'it is readonly', 0, 4),
        (3, 'Func<T>', 0, 1),
        (3, 'Action', 0, 2),
        (3, 'Delegate', 0, 3),
        (3, 'Expression<Func<T>>', 1, 4),
        (4, 'Invoke()', 0, 1),
        (4, 'Parse()', 0, 2),
        (4, 'Compile()', 1, 3),
        (4, 'Build()', 0, 4),
        (5, 'heap allocation', 1, 1),
        (5, 'thread creation', 0, 2),
        (5, 'boxing', 0, 3),
        (5, 'file handles', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 21 (Insane): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 21', 'Insane', 'Approved', '2026-11-12T09:20:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 21'
  );
UPDATE quizzes
SET difficulty = 'Insane', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 21'
  AND (difficulty <> 'Insane' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Insane'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 21'
  )
  AND difficulty <> 'Insane';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 21' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('In a for loop, what does a lambda capture when it uses the loop variable?', 'A for loop reuses one variable, so every closure sees the final value.', 1),
        ('Which fix gives each closure its own loop value?', 'A fresh local per iteration gives every closure an independent copy.', 2),
        ('Which type is exact for money calculations?', 'decimal is a base-10 type, so values like 0.10 stay exact.', 3),
        ('What does 0.1 + 0.2 == 0.3 return when the values are double?', 'Binary floating point cannot represent 0.1 exactly, so the sum differs from 0.3.', 4),
        ('Which type keeps about 28 significant digits?', 'decimal offers 28 to 29 significant digits; double offers about 15 to 17.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Insane', q.position, '2026-11-12T09:20:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 21' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'a copy per iteration', 0, 1),
        (1, 'nothing', 0, 2),
        (1, 'the same single variable', 1, 3),
        (1, 'a constant', 0, 4),
        (2, 'capture the index directly', 0, 1),
        (2, 'copy the variable inside the loop', 1, 2),
        (2, 'make the lambda static', 0, 3),
        (2, 'move the loop to a method', 0, 4),
        (3, 'double', 0, 1),
        (3, 'float', 0, 2),
        (3, 'decimal', 1, 3),
        (3, 'int', 0, 4),
        (4, 'true', 0, 1),
        (4, 'an exception', 0, 2),
        (4, 'false', 1, 3),
        (4, 'a compiler error', 0, 4),
        (5, 'float', 0, 1),
        (5, 'double', 0, 2),
        (5, 'decimal', 1, 3),
        (5, 'long', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 22 (Insane): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 22', 'Insane', 'Approved', '2026-11-12T09:21:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 22'
  );
UPDATE quizzes
SET difficulty = 'Insane', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 22'
  AND (difficulty <> 'Insane' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Insane'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 22'
  )
  AND difficulty <> 'Insane';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 22' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('What does the in parameter modifier pass?', 'in passes a reference that the callee may read but not write.', 1),
        ('Which struct modifier avoids hidden defensive copies?', 'A readonly struct promises immutability, so the compiler can skip the copies.', 2),
        ('Which strings are interned by the runtime automatically?', 'Literal strings are interned at compile time; runtime-built strings are not.', 3),
        ('How long does an interned string live?', 'The intern pool is never collected while the process runs.', 4),
        ('Which JIT tier recompiles hot methods with more optimizations?', 'Tier 1 recompiles frequently used methods with the full optimizer.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Insane', q.position, '2026-11-12T09:21:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 22' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'a copy', 0, 1),
        (1, 'a pointer', 0, 2),
        (1, 'a readonly reference', 1, 3),
        (1, 'a box', 0, 4),
        (2, 'partial', 0, 1),
        (2, 'sealed', 0, 2),
        (2, 'readonly', 1, 3),
        (2, 'static', 0, 4),
        (3, 'none', 0, 1),
        (3, 'literals', 1, 2),
        (3, 'concatenations at runtime', 0, 3),
        (3, 'all strings', 0, 4),
        (4, 'until the next GC', 0, 1),
        (4, 'one method call', 0, 2),
        (4, 'for the process lifetime', 1, 3),
        (4, 'one second', 0, 4),
        (5, 'tier 0', 0, 1),
        (5, 'the interpreter', 0, 2),
        (5, 'tier 1', 1, 3),
        (5, 'the linker', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 23 (Insane): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 23', 'Insane', 'Approved', '2026-11-12T09:22:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 23'
  );
UPDATE quizzes
SET difficulty = 'Insane', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 23'
  AND (difficulty <> 'Insane' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Insane'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 23'
  )
  AND difficulty <> 'Insane';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 23' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('What does a padding oracle reveal to an attacker?', 'Distinct error behavior for bad padding lets the attacker recover plaintext block by block.', 1),
        ('Which cipher mode authenticates data and avoids padding oracles?', 'GCM authenticates the ciphertext and needs no padding at all.', 2),
        ('Which comparison avoids leaking a secret byte by byte?', 'A constant-time compare touches every byte regardless of where the first difference is.', 3),
        ('What does a timing attack measure?', 'Small time differences between branches leak information about the data being processed.', 4),
        ('Which code pattern invites timing leaks on secrets?', 'Returning at the first mismatch makes the comparison time depend on the secret.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Insane', q.position, '2026-11-12T09:22:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 23' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'the encryption key', 0, 1),
        (1, 'the plaintext length', 0, 2),
        (1, 'whether padding is valid', 1, 3),
        (1, 'the IV', 0, 4),
        (2, 'ECB', 0, 1),
        (2, 'CBC', 0, 2),
        (2, 'stream XOR', 0, 3),
        (2, 'AEAD with GCM', 1, 4),
        (3, 'early exit compare', 0, 1),
        (3, 'string compare', 0, 2),
        (3, 'constant-time compare', 1, 3),
        (3, 'case-insensitive compare', 0, 4),
        (4, 'disk usage', 0, 1),
        (4, 'response time differences', 1, 2),
        (4, 'packet size', 0, 3),
        (4, 'memory layout', 0, 4),
        (5, 'comparing all bytes every time', 0, 1),
        (5, 'hashing the secret', 0, 2),
        (5, 'comparing with early exit', 1, 3),
        (5, 'using fixed-length keys', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 24 (Insane): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 24', 'Insane', 'Approved', '2026-11-12T09:23:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 24'
  );
UPDATE quizzes
SET difficulty = 'Insane', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 24'
  AND (difficulty <> 'Insane' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Insane'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 24'
  )
  AND difficulty <> 'Insane';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 24' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which attack makes the victim browser send an unintended authenticated request?', 'CSRF abuses the cookies the browser attaches automatically to cross-site requests.', 1),
        ('Which attack runs attacker script inside the victim browser?', 'XSS injects script that the victim browser then executes in the site context.', 2),
        ('Which attack makes the server fetch an attacker-chosen internal URL?', 'SSRF turns the server into a proxy for requests to internal services.', 3),
        ('Which control blocks cross-site request forgery?', 'The token travels in the request body or header, which a cross-site form cannot guess.', 4),
        ('Why rotate encryption keys periodically?', 'Rotation bounds how much data one leaked key can expose.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Insane', q.position, '2026-11-12T09:23:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 24' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'XSS', 0, 1),
        (1, 'CSRF', 1, 2),
        (1, 'SSRF', 0, 3),
        (1, 'SQL injection', 0, 4),
        (2, 'CSRF', 0, 1),
        (2, 'SSRF', 0, 2),
        (2, 'XSS', 1, 3),
        (2, 'clickjacking', 0, 4),
        (3, 'CSRF', 0, 1),
        (3, 'XSS', 0, 2),
        (3, 'SSRF', 1, 3),
        (3, 'open redirect', 0, 4),
        (4, 'CORS headers', 0, 1),
        (4, 'a CAPTCHA', 0, 2),
        (4, 'an anti-CSRF token', 1, 3),
        (4, 'longer sessions', 0, 4),
        (5, 'to speed up encryption', 0, 1),
        (5, 'to shrink the value of a leaked key', 1, 2),
        (5, 'to reduce storage', 0, 3),
        (5, 'to avoid TLS', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);

-- ----------------------------------------------------------------------------
-- Level 25 (Insane): 5 questions, 4 choices each, one correct.
-- ----------------------------------------------------------------------------
INSERT INTO quizzes (topic_id, title, difficulty, status, created_at)
SELECT topic_id, 'Level 25', 'Insane', 'Approved', '2026-11-12T09:24:00Z'
FROM topics
WHERE name = 'Campaign'
  AND NOT EXISTS (
    SELECT 1 FROM quizzes q
    JOIN topics t ON t.topic_id = q.topic_id
    WHERE t.name = 'Campaign' AND q.title = 'Level 25'
  );
UPDATE quizzes
SET difficulty = 'Insane', status = 'Approved'
WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
  AND title = 'Level 25'
  AND (difficulty <> 'Insane' OR status <> 'Approved');
UPDATE questions
SET difficulty = 'Insane'
WHERE quiz_id = (
    SELECT quiz_id FROM quizzes
    WHERE topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign') AND title = 'Level 25'
  )
  AND difficulty <> 'Insane';
INSERT INTO questions (quiz_id, question_text, explanation, difficulty, position, created_at)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 25' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), q(question_text, explanation, position) AS (
    VALUES
        ('Which flaw lets a user act beyond their role?', 'Authentication proves who the user is; authorization must still check what the user may do.', 1),
        ('Which pattern leaks event handlers?', 'A forgotten subscription keeps the subscriber reachable through the publisher.', 2),
        ('What keeps a static event subscriber alive?', 'A static publisher lives for the process and roots every handler still attached to it.', 3),
        ('Which operator removes an event handler?', 'The -= operator removes a matching handler from the invocation list.', 4),
        ('Which design avoids event lifetime leaks?', 'Weak events keep subscribers alive only while someone else still references them.', 5)
)
SELECT z.quiz_id, q.question_text, q.explanation, 'Insane', q.position, '2026-11-12T09:24:00Z'
FROM z JOIN q
WHERE NOT EXISTS (
    SELECT 1 FROM questions x WHERE x.quiz_id = z.quiz_id AND x.position = q.position
);
INSERT INTO choices (question_id, choice_text, is_correct, position)
WITH z(quiz_id) AS (
    SELECT quiz_id FROM quizzes
    WHERE title = 'Level 25' AND topic_id = (SELECT topic_id FROM topics WHERE name = 'Campaign')
), c(question_position, choice_text, is_correct, position) AS (
    VALUES
        (1, 'slow queries', 0, 1),
        (1, 'missing authorization checks', 1, 2),
        (1, 'weak hashing', 0, 3),
        (1, 'verbose errors', 0, 4),
        (2, 'unsubscribing in Dispose', 0, 1),
        (2, 'subscribing without unsubscribing', 1, 2),
        (2, 'using weak events', 0, 3),
        (2, 'one handler per button', 0, 4),
        (3, 'the garbage collector', 0, 1),
        (3, 'the stack', 0, 2),
        (3, 'the publisher event reference', 1, 3),
        (3, 'a weak reference', 0, 4),
        (4, '+=', 0, 1),
        (4, '=>', 0, 2),
        (4, '-=', 1, 3),
        (4, '==', 0, 4),
        (5, 'strong references', 0, 1),
        (5, 'public fields', 0, 2),
        (5, 'weak events', 1, 3),
        (5, 'static callbacks', 0, 4)
)
SELECT q.question_id, c.choice_text, c.is_correct, c.position
FROM z
JOIN questions q ON q.quiz_id = z.quiz_id
JOIN c ON c.question_position = q.position
WHERE NOT EXISTS (
    SELECT 1 FROM choices x WHERE x.question_id = q.question_id AND x.position = c.position
);
