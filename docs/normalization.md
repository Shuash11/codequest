# CodeQuest — Database Normalization (1NF → 2NF → 3NF)

*Final (increment I6, 2026-11-08) — written against `database/schema.sql` (the
deployed schema is the single source of truth). The analysis below was drafted
in phase P1 and re-verified against the unchanged schema at delivery.*

## 1. Purpose and scope

The exam brief requires a relational database that is **normalised up to Third
Normal Form (3NF)** and a written explanation of that normalisation that the
student can defend in the code review (`docs/oop-final-exam-guidelines.md`
lines 17-19, 93-99, 147-148, 170-171).

This document is that explanation. It walks the CodeQuest database from the
flat, "everything in one table" form a beginner would write, through 1NF, 2NF
and 3NF, and ends with a per-table proof that the deployed schema
(`database/schema.sql`) is in 3NF.

**Scope.** The seven tables of the CodeQuest database are covered:

| Table | What it stores | Key |
|---|---|---|
| `users` | the local account that logs in | `user_id` |
| `topics` | the subject areas a quiz belongs to | `topic_id` |
| `quizzes` | the quiz header: topic, difficulty, review status | `quiz_id` |
| `questions` | the questions of one quiz | `question_id` |
| `choices` | the four answer options of one question, with the answer key | `choice_id` |
| `quiz_attempts` | one completed run of a quiz by one user | `attempt_id` |
| `submitted_answers` | what the student picked for each answered question | `(attempt_id, question_id)` |

**Out of scope.** The normal forms describe how the *data* is organised. They do
not say anything about the C# code — the object-oriented design is documented
separately in `docs/oop-explanation.md` (phase P17).

## 2. The seven tables at a glance

```
   content chain (what a quiz consists of)      history chain (what the student did)
   ----------------------------------------      -------------------------------------

   topics  1 ---- n  quizzes  1 ---- n  questions  1 ---- n  choices
                 ^                         ^
                 |                         |  every submitted answer points at
                 |                         |  the question that was answered
                 |  a quiz is attempted     |
                 |  many times              |
           quiz_attempts  1 ---- n  submitted_answers
                 ^
                 |  a user takes many attempts
               users
```

* `topics` → `quizzes` → `questions` → `choices` is the **content chain**: a
  topic holds quizzes, a quiz holds questions, a question holds its options.
* `users` → `quiz_attempts` → `submitted_answers` is the **history chain**: a
  user takes quizzes, an attempt stores the answers that were given.
* `submitted_answers` joins both chains: it points at the attempt, at the
  question that was answered, and at the option that was chosen.

The detailed crow's-foot diagram is in [`docs/erd.md`](erd.md).

## 3. How to read this document (notation)

| Notation | Meaning |
|---|---|
| `X → Y` | **functional dependency**: if `X` is known, `Y` is determined — there can only ever be one value of `Y` for a given `X`. |
| `X →+ Y` | `X` determines `Y` **transitively** (`X → Z → Y`). This is the thing 3NF forbids. |
| **candidate key** | a minimal set of columns that uniquely identifies a row (the primary key, or an alternate key such as a `UNIQUE` column). |
| **prime attribute** | a column that is part of *some* candidate key. |
| **non-key attribute** | a column that is *not* part of any candidate key. |
| **partial dependency** | a non-key attribute depends on only *part* of a composite key. This is what 2NF forbids. |
| **transitive dependency** | a non-key attribute depends on another non-key attribute. This is what 3NF forbids. |

The three normal forms, in one line each:

| Form | The rule |
|---|---|
| **1NF** | every column holds a single atomic value, and there are no repeating groups of rows. |
| **2NF** | 1NF **and** no non-key attribute depends on only part of a composite key. (A table whose key is a single column is automatically in 2NF.) |
| **3NF** | 2NF **and** no non-key attribute depends on another non-key attribute (no transitive dependency). |

## 4. Step 0 — the unnormalized starting point

The first version of a quiz database is usually one wide table where one row
describes a whole quiz, with the questions and the options repeated inside the
row as columns:

| quiz_id | title | topic_name | q1 | a1 | b1 | c1 | d1 | correct1 | q2 | a2 | … |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | C# Core Concepts | C# | What is a `const`? | static | const | readonly | sealed | const | Which collection… | … | … |

This form is **not even in 1NF**:

* the columns `q1`, `q2`, `q3`, … are a **repeating group** — the number of
  questions would force the table to be changed for every new quiz;
* the columns `a1`, `b1`, `c1`, `d1` are a second repeating group for the
  options;
* `correct1` repeats the text of the correct option, which is already stored
  in one of the four option columns — duplicated data that can disagree with
  itself;
* everything about one quiz (title, topic name, difficulty) is repeated on
  every row once the table is copied per question.

The normalisation steps below remove exactly these problems.

## 5. Step 1 — First Normal Form (atomic values, no repeating groups)

**The 1NF form.** The two repeating groups are flattened into rows: one row
per *question-option pair*. Every column now holds a single atomic value.

```
QUIZ_FLAT(
    quiz_id, title, topic_id, topic_name, difficulty, status, quiz_created_at,
    question_no, question_text, explanation, question_created_at,
    choice_no, choice_text, is_correct
)
Primary key of QUIZ_FLAT: (quiz_id, question_no, choice_no)
```

**What was done for each of the seven final tables to reach 1NF:**

| Table | Repeating group that was removed | Atomic-value check |
|---|---|---|
| `users` | none — one row per account | `username`, `display_name` and `password_hash` each hold one value; the password is stored as one hash string, never as a list of hashed variants. |
| `topics` | none — one row per subject | `category` holds one value out of `Programming` / `Cybersecurity` (a `CHECK` constraint), never two. |
| `quizzes` | the list of questions of the quiz (`q1, q2, …`) | `title`, `difficulty` and `status` hold one value each. |
| `questions` | the list of options of the question (`a, b, c, d`) | `question_text` and `explanation` are single strings, never a delimited list. |
| `choices` | the "correct option text" column that repeated one of the four options | `is_correct` is a single flag `0` or `1`; the correct text is **not** copied into the question row. |
| `quiz_attempts` | the list of answers given in the attempt | `score` is one number (correct answers), `attempted_at` is one timestamp. |
| `submitted_answers` | none — one row per answered question | `chosen_choice_id` points at exactly one option. |

**Result.** 1NF holds for the whole database: every value in every cell is
atomic, and no table contains a repeating group of columns.

## 6. Step 2 — Second Normal Form (no partial dependency on a composite key)

In `QUIZ_FLAT` the key is the composite `(quiz_id, question_no, choice_no)`.
Several non-key attributes depend on only **part** of that key — that is the
2NF violation:

| Non-key attribute of `QUIZ_FLAT` | Depends on | Part of the key? | Action |
|---|---|---|---|
| `title`, `difficulty`, `status`, `quiz_created_at` | `quiz_id` | yes — partial | moved into **`quizzes`** (key `quiz_id`) |
| `question_text`, `explanation`, `question_created_at` | `(quiz_id, question_no)` | yes — partial | moved into **`questions`** (key `question_id`, plus the unique pair `(quiz_id, position)`) |
| `topic_id` | `quiz_id` | yes — partial | kept as a **foreign key** in `quizzes`; the topic's own data is split off in step 3 |
| `choice_text`, `is_correct` | `(quiz_id, question_no, choice_no)` | no — the whole key | stay together in **`choices`** (key `choice_id`, plus the unique pair `(question_id, choice_text)`) |

Three more tables have to exist before the history part of the database is in
2NF at all:

* **`users`** — one row per account, key `user_id`. It is a single-column key,
  so 2NF is automatic.
* **`quiz_attempts`** — one row per completed run, key `attempt_id`. Also a
  single-column key.
* **`submitted_answers`** — the answers of an attempt were still inside the
  attempt row. They become their own table with the composite key
  `(attempt_id, question_id)`: a question is answered **once per attempt**, but
  the same question may be answered in many different attempts.

**The 2NF check of the final schema.** For every table whose key has more than
one column, no non-key attribute depends on only one part of it:

| Table | Candidate key | Does any non-key attribute depend on only part of it? |
|---|---|---|
| `quizzes` | `(topic_id, title)` | **No.** `difficulty`, `status` and `created_at` depend on the *pair* — one topic holds many quizzes with different difficulties and statuses, and a title may exist under several topics. |
| `questions` | `(quiz_id, position)` | **No.** `question_text`, `explanation` and `created_at` depend on the pair; `quiz_id` alone does not determine the text (a quiz holds several questions), and `position` alone does not either. |
| `choices` | `(question_id, choice_text)` | **No.** `is_correct` and `position` depend on the pair — which option is correct is only meaningful together with its question. |
| `submitted_answers` | `(attempt_id, question_id)` | **No.** `chosen_choice_id` depends on the pair — the option a student picked for *that* question in *that* attempt. |
| `users`, `topics`, `quiz_attempts` (single-column primary key and no composite candidate key) | `user_id`, `topic_id`, `attempt_id` | **Not possible** — with a one-column key there is no "part" of the key, so 2NF holds automatically. |

**Result.** 2NF holds for every table.

## 7. Step 3 — Third Normal Form (no transitive dependency)

2NF removed the *partial* dependencies. The remaining problem is a
**transitive dependency**: a non-key attribute that is determined by *another
non-key attribute*.

### 7.1 `quiz_id → topic_id → topic_name` — the topic's data is split off

In the 2NF design the quiz row still carried the name, category and
description of its topic:

```
quizzes(quiz_id, topic_id, topic_name, topic_category, topic_description, title, …)
        quiz_id → topic_id → topic_name, topic_category, topic_description
```

`topic_id` is a non-key attribute of `quizzes`, and it determined three more
non-key attributes — a textbook transitive dependency. The fix is the
**`topics`** table: `quizzes` keeps `topic_id` only, as a foreign key, and the
topic's own data is stored exactly once.

The same reasoning applies everywhere else in the schema:

* `question_id → quiz_id → title` — the quiz's title is **not** repeated in
  `questions`; it is fetched by joining `questions → quizzes`;
* `chosen_choice_id → question_id → question_text` — the question text is
  **not** repeated in `submitted_answers`.

> **A foreign key is not a determinant.** `quizzes.topic_id` does not determine
> any other column of `quizzes` (one topic holds many quizzes), and
> `questions.quiz_id` does not determine any other column of `questions` (one
> quiz holds many questions). A foreign key is a *reference* to another table,
> not a hidden key of its own — otherwise no foreign key could ever exist in a
> normalised database.

### 7.2 `question_id → is_correct` — the answer key is not copied into the answers

The 2NF answer row still wanted to remember whether the pick was right:

```
submitted_answers(attempt_id, question_id, chosen_choice_id, is_correct)
```

`is_correct` is determined by the question and the chosen option, and both
already exist in `choices`:

```
(question_id, chosen_choice_id) → is_correct        (in choices)
```

Copying that flag into every answer row would create the transitive dependency
`question_id →+ is_correct` and would allow the copy to disagree with the real
answer key. Therefore **`submitted_answers` has no `is_correct` column at
all**. Correctness is *derived* at read time by joining `submitted_answers` to
`choices` — this is exactly what the scoring service (P3) and the answer
review screen (P10) do.

### 7.3 `quiz_id → total_questions → percentage` — the report figures are not stored

An attempt row is tempting to store as
`quiz_attempts(attempt_id, quiz_id, user_id, score, total_questions, percentage)`.
That would be wrong twice:

* `total_questions` is determined by the quiz (`quiz_id → total_questions`), so
  it is a transitive dependency — and it would also go stale the moment a
  question is added to the quiz;
* `percentage` is determined by `score` and `total_questions`, so it is a
  *derived value* that would have to be kept consistent with both.

The schema stores only what is a fact of the attempt itself: **`score` (the
number of correct answers)**. The number of answered questions is obtained by
counting the rows of `submitted_answers`, and the percentage is calculated in
the report service (P11). Nothing is stored twice.

### 7.4 The check for every table

For every table: does any non-key attribute determine another non-key
attribute?

| Table | Non-key attributes | Do they determine each other? |
|---|---|---|
| `users` | `username`, `password_hash`, `display_name`, `created_at` | **No** — every one of them is directly determined by `user_id`. |
| `topics` | `name`, `category`, `description`, `created_at` | **No** — each is directly determined by `topic_id`. |
| `quizzes` | `title`, `difficulty`, `status`, `created_at` (plus the foreign key `topic_id`) | **No** — `topic_id` determines nothing else in this table; a topic has many quizzes. |
| `questions` | `question_text`, `explanation`, `position`, `created_at` (plus `quiz_id`) | **No** — `quiz_id` determines nothing else here; a quiz has many questions. |
| `choices` | `choice_text`, `is_correct`, `position` (plus `question_id`) | **No** — `question_id` determines nothing else here; a question has many options. |
| `quiz_attempts` | `score`, `attempted_at` (plus `quiz_id`, `user_id`) | **No** — neither foreign key determines anything else here, and `score` is not derived from another stored attribute. |
| `submitted_answers` | `chosen_choice_id` (plus `attempt_id`, `question_id`) | **No** — the key attributes identify *which* answer this is; no column of the row determines another column of the row. |

**Result.** 3NF holds for every table. In fact every determinant in the schema
is a candidate key, so the database also satisfies **BCNF** — the stricter
form of 3NF.

## 8. Functional dependency catalogue

The full list of dependencies of the deployed schema. "Determinant" means the
column(s) on the left of the arrow; in every row below the determinant is a
candidate key, which is what makes the table 3NF (and BCNF).

### `users`

| | |
|---|---|
| Columns | `user_id` (PK), `username` (UNIQUE), `password_hash`, `display_name`, `created_at` |
| Candidate keys | `user_id` (primary), `username` (alternate) |
| Prime attributes | `user_id`, `username` |
| Dependencies | `user_id → username, password_hash, display_name, created_at`<br>`username → user_id, password_hash, display_name, created_at` |
| Non-key attributes | `password_hash`, `display_name`, `created_at` — each determined directly by the key; none determines another. |

### `topics`

| | |
|---|---|
| Columns | `topic_id` (PK), `name` (UNIQUE), `category`, `description`, `created_at` |
| Candidate keys | `topic_id` (primary), `name` (alternate) |
| Prime attributes | `topic_id`, `name` |
| Dependencies | `topic_id → name, category, description, created_at`<br>`name → topic_id, category, description, created_at` |
| Non-key attributes | `category`, `description`, `created_at` — each determined directly by the key. |

### `quizzes`

| | |
|---|---|
| Columns | `quiz_id` (PK), `topic_id`, `title`, `difficulty`, `status`, `created_at`; `UNIQUE(topic_id, title)` |
| Candidate keys | `quiz_id` (primary), `(topic_id, title)` (alternate) |
| Prime attributes | `quiz_id`, `topic_id`, `title` |
| Dependencies | `quiz_id → topic_id, title, difficulty, status, created_at`<br>`(topic_id, title) → quiz_id, difficulty, status, created_at` |
| Important negatives | `topic_id` alone does **not** determine `title`, `difficulty` or `status` (a topic holds many quizzes) — therefore `topic_id` is not a determinant here, only a reference to `topics`. |

### `questions`

| | |
|---|---|
| Columns | `question_id` (PK), `quiz_id`, `question_text`, `explanation`, `position`, `created_at`; `UNIQUE(quiz_id, position)` |
| Candidate keys | `question_id` (primary), `(quiz_id, position)` (alternate) |
| Prime attributes | `question_id`, `quiz_id`, `position` |
| Dependencies | `question_id → quiz_id, question_text, explanation, position, created_at`<br>`(quiz_id, position) → question_id, question_text, explanation, created_at` |
| Important negatives | `quiz_id` alone does **not** determine `question_text` (a quiz holds several questions), and `position` alone does not either. |

### `choices`

| | |
|---|---|
| Columns | `choice_id` (PK), `question_id`, `choice_text`, `is_correct`, `position`; `UNIQUE(question_id, choice_text)`; `UNIQUE(question_id, choice_id)` (parent key of the composite foreign key) |
| Candidate keys | `choice_id` (primary), `(question_id, choice_text)` (alternate) |
| Prime attributes | `choice_id`, `question_id`, `choice_text` |
| Dependencies | `choice_id → question_id, choice_text, is_correct, position`<br>`(question_id, choice_text) → choice_id, is_correct, position` |
| Important negatives | `question_id` alone does **not** determine `is_correct`: of the four rows of one question exactly one has `is_correct = 1` and three have `0`. The business rule "exactly one correct option per question" is enforced by the question editor (P8) and checked by the seed test, because plain SQL cannot express "exactly one row in this group". |

### `quiz_attempts`

| | |
|---|---|
| Columns | `attempt_id` (PK), `quiz_id`, `user_id`, `score`, `attempted_at` |
| Candidate keys | `attempt_id` (primary) |
| Prime attributes | `attempt_id` |
| Dependencies | `attempt_id → quiz_id, user_id, score, attempted_at` |
| Important negatives | `quiz_id` alone does **not** determine `score` — the same quiz can be attempted with different results; `user_id` alone does not either. The same user may retake the same quiz, so there is deliberately **no** `UNIQUE(user_id, quiz_id)`. |

### `submitted_answers`

| | |
|---|---|
| Columns | `attempt_id`, `question_id`, `chosen_choice_id`; `PRIMARY KEY (attempt_id, question_id)` |
| Candidate keys | `(attempt_id, question_id)` (primary) |
| Prime attributes | `attempt_id`, `question_id` |
| Dependencies | `(attempt_id, question_id) → chosen_choice_id` |
| Important negatives | `question_id` alone does **not** determine `chosen_choice_id` (two students, or the same student twice, may pick different options). `attempt_id` alone does not determine `question_id`. |

## 9. Foreign key relationships

The rubric asks for at least three related tables with proper foreign keys
(`docs/oop-final-exam-guidelines.md` lines 32-34, 48, 98). CodeQuest declares
**nine foreign key constraints across eight parent-child relationships**:

| # | Child column | → Parent column | ON DELETE | Why this action was chosen | Index on the child column |
|---|---|---|---|---|---|
| 1 | `quizzes.topic_id` | `topics.topic_id` | `RESTRICT` | A topic that still has quizzes must not be deleted — the delete is blocked and the UI shows a friendly message (P8) instead of orphaning quizzes. | `idx_quizzes_topic_id` |
| 2 | `questions.quiz_id` | `quizzes.quiz_id` | `CASCADE` | A question is meaningless without its quiz, so deleting a quiz that was never attempted removes its questions and their options. | `idx_questions_quiz_id` |
| 3 | `choices.question_id` | `questions.question_id` | `CASCADE` | An option is meaningless without its question. | `idx_choices_question_id` |
| 4 | `quiz_attempts.quiz_id` | `quizzes.quiz_id` | `RESTRICT` | An attempt is history. A quiz that has been attempted must not be deleted, so the user's record can never disappear. | `idx_quiz_attempts_quiz_id` |
| 5 | `quiz_attempts.user_id` | `users.user_id` | `RESTRICT` | The same rule protects the user's own history. | `idx_quiz_attempts_user_id` |
| 6 | `submitted_answers.attempt_id` | `quiz_attempts.attempt_id` | `CASCADE` | An answer row only exists inside its attempt, so removing the attempt removes its answers. | `idx_submitted_answers_attempt_id` |
| 7 | `submitted_answers.question_id` | `questions.question_id` | `RESTRICT` | An answered question is part of the history: it must not be deleted while an answer still points at it. | `idx_submitted_answers_question_id` |
| 8 | `submitted_answers.chosen_choice_id` | `choices.choice_id` | `RESTRICT` | The option that was picked must stay in the database, otherwise the history would lose its meaning. | `idx_submitted_answers_choice_id` |
| 9 | `submitted_answers.(question_id, chosen_choice_id)` | `choices.(question_id, choice_id)` | `RESTRICT` | **Composite foreign key.** It proves that the chosen option really belongs to the answered question — a student can never record a choice from a different question. | covered by `idx_submitted_answers_question_id` and the unique parent key |

**Summary of the delete strategy**

* `CASCADE` is used **only** where the child row is meaningless without its
  parent: question → quiz, choice → question, answer → attempt.
* `RESTRICT` is used wherever the child row is a **historical record** that must
  survive: attempt → quiz, attempt → user, answer → question, answer → choice,
  quiz → topic. The blocked delete is what the management screens (P8) turn
  into a friendly message.
* The effect of the two rules together: a Draft quiz with no attempts can be
  deleted cleanly (questions and choices cascade away), while a quiz that was
  already taken cannot be deleted at all.

**SQLite requirement.** SQLite does **not** enforce foreign keys unless
`PRAGMA foreign_keys = ON` is executed on **every** connection
(<https://sqlite.org/foreignkeys.html> §2). `database/schema.sql` switches it
on for the script itself, but the application must repeat it for each opened
connection — that is the connection factory's job in phase P4
(`Data/Db.cs`). The P1 acceptance test proves the difference: the very same
`INSERT` fails with the pragma on and succeeds with the pragma off.

## 10. 3NF proof summary

| Table | 1NF (atomic, no repeating groups) | 2NF (no partial dependency) | 3NF (no transitive dependency) | Verdict |
|---|---|---|---|---|
| `users` | yes — one value per cell, no repeating group | yes — single-column key | yes — no non-key attribute determines another | **3NF** (also BCNF) |
| `topics` | yes | yes — single-column key | yes | **3NF** (also BCNF) |
| `quizzes` | yes — questions moved to their own table | yes — `difficulty`, `status`, `created_at` depend on the whole key `(topic_id, title)` | yes — `topic_id` determines no other column of the table | **3NF** (also BCNF) |
| `questions` | yes — options moved to their own table | yes — text and explanation depend on the whole key `(quiz_id, position)` | yes — `quiz_id` determines no other column of the table | **3NF** (also BCNF) |
| `choices` | yes — one row per option, `is_correct` is a flag | yes — `is_correct` depends on the whole key `(question_id, choice_text)` | yes — the answer key is stored once, in one place only | **3NF** (also BCNF) |
| `quiz_attempts` | yes — one row per run, no list of answers | yes — single-column key | yes — `score` is a fact of the attempt; `total_questions` and `percentage` are derived and not stored | **3NF** (also BCNF) |
| `submitted_answers` | yes — one row per answered question | yes — `chosen_choice_id` depends on the whole key `(attempt_id, question_id)` | yes — `is_correct` is not copied into the answer rows | **3NF** (also BCNF) |

**Formal statement.** In every relation of the CodeQuest database:

1. every attribute is atomic (1NF);
2. every determinant is a candidate key, and no non-key attribute depends on
   only part of a composite key (2NF);
3. no non-key attribute is determined by another non-key attribute (3NF).

Because every determinant is a candidate key, the schema satisfies **BCNF**,
which is stronger than 3NF — so the database is comfortably inside the
"normalised up to 3NF" requirement of the exam brief.

## 11. Deliberate design decisions (what is *not* stored, and why)

| Decision | Reason | Who must follow it |
|---|---|---|
| `quiz_attempts` stores **only** `score` (number of correct answers) — no `total_questions`, no `percentage`. | `total_questions` would be a transitive dependency of `quiz_id` and would go stale when a question is added; `percentage` is a derived value. The number of answered questions comes from counting `submitted_answers`. | P9 (result screen) and P11 (report service) must derive the denominator and the percentage. |
| `submitted_answers` has **no** `is_correct` column. | The answer key belongs to the option, not to the answer. Correctness is derived by joining `choices`. | P9 (scoring), P10 (answer review) and P11 (reports) must join `choices`. |
| `quizzes` has **no** `approved_at` column and **no** `approved_by`. | `status` is the whole workflow state (`Draft` → `Approved`). An approval timestamp would be a second, easily inconsistent copy of the same fact. | P13 (review screen) only flips `status`. |
| `password_hash` is stored in the format `pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>` (PBKDF2-HMAC-SHA256, 600,000 iterations, random per-user salt); the plaintext is never stored. | The login form (P7) must derive the key from the typed password in exactly the same way and compare the derived key with the stored hash. The seed's demo password is `codequest123`. | **P7 must use the same format**, otherwise the seeded user cannot log in. |
| Surrogate keys (`INTEGER PRIMARY KEY`) next to natural keys (`username`, `name`, `(topic_id, title)`, `(quiz_id, position)`, `(question_id, choice_text)`). | Surrogate keys keep the foreign keys short and stable; the natural keys let the application reject duplicates ("two quizzes with the same title under one topic"). | P8 (management screens) should show the natural key in the validation messages. |
| `explanation` is `NOT NULL`. | The answer review (P10) shows an explanation for every question; an empty explanation is stored as an empty string rather than as `NULL`. | P8 (question editor) must supply a value. |
| `position` columns in `questions` and `choices`. | The quiz player needs a stable order, and a separate column is the normalised way to store it (a "1., 2., 3." text column would be a repeating group). | P8 must keep the positions unique inside their parent. |
| `category` is limited to `Programming` / `Cybersecurity`. | It matches the two halves of the project's subject matter (proposal line 7) and gives the report screens a simple grouping. | P8 may add categories — that is a `CHECK` constraint change. |
| The rule "the answered question must belong to the quiz of the attempt" is **not** a database constraint. | Expressing it would mean repeating `quiz_id` inside `submitted_answers`, which is exactly the transitive dependency that 3NF forbids. | **P4 (data access layer) must validate it** before writing an answer row. |
| "Exactly one correct option per question" is **not** a database constraint. | A plain `CHECK` constraint cannot count the rows of a group. | P8 (question editor) must enforce it; the seed test proves the sample data obeys it. |

## 12. How this was verified

The statements above are not theory: they were executed against a real SQLite
database (Python 3.12 `sqlite3` module, SQLite 3.45.3) in phase P1.

| Test | How it was run | Result |
|---|---|---|
| Fresh database | `schema.sql` then `seed.sql` executed on a new empty database file | both scripts ran without an error; `PRAGMA integrity_check` = `ok`; `PRAGMA foreign_key_check` = empty |
| Foreign keys enforced | `INSERT INTO questions … VALUES (…, quiz_id = 9999, …)` with `PRAGMA foreign_keys = ON` | **rejected** — `IntegrityError: FOREIGN KEY constraint failed` |
| Foreign keys not enforced | the same `INSERT` on a copy of the seeded database with `PRAGMA foreign_keys = OFF` | **accepted** — the row was written (rowid assigned) |
| `quizzes.status` | `INSERT … status = 'Published'` (also `'approved'`, `'draft'`, `'ARCHIVED'`) | **rejected** — `IntegrityError: CHECK constraint failed: status IN ('Draft', 'Approved')` |
| Explicit primary keys | `PRAGMA table_info` for all seven tables | every table has a primary key; six single-column `INTEGER PRIMARY KEY`, one composite `PRIMARY KEY (attempt_id, question_id)` |
| Foreign key inventory | `PRAGMA foreign_key_list` for all seven tables | 9 foreign key constraints over 8 parent-child relationships (listed in section 9); the pragma prints 10 rows, because the composite foreign key on `submitted_answers` spans two columns (seq 0 and 1) under one constraint id |
| Indexes on child keys | `PRAGMA index_list` / `PRAGMA index_info` | an index exists on every child foreign key column |
| Answer key | `choices` grouped by `question_id` | every one of the 21 questions has exactly 4 options and exactly one with `is_correct = 1` |
| Attempt consistency | `quiz_attempts.score` compared with the number of correct picks in `submitted_answers` | all four attempts match (4, 2, 5, 3) |

**Reproduce it** — from the repository root (the scratch database is written to
the system temp folder and removed again, so the repository stays clean):

```powershell
python -c "import sqlite3, tempfile, os; p = os.path.join(tempfile.gettempdir(), 'codequest-demo.db'); c = sqlite3.connect(p); c.executescript(open('database/schema.sql', encoding='utf-8').read()); c.executescript(open('database/seed.sql', encoding='utf-8').read()); print(c.execute('PRAGMA integrity_check').fetchone()[0]); c.close(); os.remove(p)"
```

The seeded numbers that the report service (P11) and the report screen (P12)
must reproduce are listed at the end of `database/seed.sql`: 4 attempts,
3 distinct quizzes, 14 correct out of 20 answered (70 % overall), C# 60 %,
Python 100 %, SQL 60 %, and two topics without attempts.
