<!-- CodeQuest — system description (increment I6 deliverable) -->

# CodeQuest — AI-Powered Programming and Cybersecurity Quiz System

## Title and brief description

**CodeQuest** is a desktop quiz application (C#/.NET Windows Forms, local
SQLite) that makes learning programming and cybersecurity fun. A student
picks a topic, a difficulty, and a question count; the system generates a
fresh quiz through a chain of free AI endpoints (NVIDIA first, OpenRouter as
fallback); the generated quiz is reviewed and approved before it becomes
takeable; taking it is auto-scored; every attempt lands in a searchable
history with full answer review, and a performance report shows what to
practice next. Everything except *generating new* quizzes works offline —
the seeded question bank, quiz-taking, scoring, history, and reports are all
local.

## Objectives

1. Make self-testing in programming and cybersecurity engaging enough to do
   regularly (fast quiz runs, instant scoring, clear explanations).
2. Keep content fresh without hand-writing every question: the system, not
   the user, authors quizzes — with a human review gate before students see
   them.
3. Work without an internet connection for everything a student needs to
   *study* (take, score, review, track), reserving the network for *new
   content only*.
4. Meet the final-exam rubric honestly: 3NF database, the four OOP concepts
   in real code, CRUD with search/filter, input validation, a report, and a
   UI a stranger can navigate.

## Features and functional modules

The seven sidebar modules (all backed by the `ModulePanel` shell contract):

1. **Take quiz** (spec 5.6/5.7) — the home screen: every approved quiz with
   its draft siblings grayed out; question-by-question run with progress;
   instant result summary with per-question verdicts.
2. **History** (spec 5.8/5.9) — past attempts with search, topic and score
   filters; every answer reviewable offline (chosen vs correct plus the
   explanation).
3. **Reports** (spec 5.10) — attempts/quizzes/average summary, per-topic
   averages with best/latest, and the topics that need more practice
   (weakest first), with topic and difficulty filters.
4. **Generate quiz** (spec 5.11) — pick topic/difficulty/count, generate
   through the provider chain; animated busy state (the call runs off the UI
   thread); typed failure banner when no provider is configured or the
   network is down — the offline path is always offered.
5. **Review** (spec 5.12) — AI-generated drafts land here; the reviewer sees
   each question with the AI's claimed answer highlighted, can edit the
   questions, approve (making the quiz takeable), or discard.
6. **Topics** (spec 5.3) — CRUD for the topic list (Programming /
   Cybersecurity categories).
7. **Quiz management** (spec 5.4/5.5) — CRUD for quizzes and their
   questions-with-choices, with live search, status/topic filters, reorder,
   and delete protection where attempts exist.

Cross-cutting: name-only sign-in (who is using the app — directive D1), a
status strip that reports success and failure for every operation and is
announced to assistive technology, confirmation dialogs for destructive
actions, and a grey/modern visual system driven entirely by design tokens.

## Technology

| Area | Choice |
|---|---|
| Language / framework | C#, .NET 10 (LTS), Windows Forms |
| Database | SQLite (local file), Microsoft.Data.Sqlite, plain ADO.NET |
| Schema | 7 tables, 3NF, foreign keys enforced per connection |
| AI | OpenAI-compatible chat completions via REST (`HttpClient`) — NVIDIA NIM primary, OpenRouter `:free` fallback, no key → offline |
| Configuration | `config/config.local.json` (git-ignored); keys never in code or logs |
| Tests | xUnit — scoring, repositories, reports, validation, filters, generation mapping |
| CI | GitHub Actions (Windows): build + test + self-contained single-file EXE artifact |
