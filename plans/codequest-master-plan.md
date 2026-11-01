# CodeQuest — Master Implementation Plan

## Status

`active` — created 2026-11-01. Adviser is the only writer; all other agents read-only.

## Task Level Declaration

**Level 3 (Complex).** Reason: a brand-new application with a relational schema and data model (SQLite, 7 tables), an external API integration (Gemini REST via HttpClient), a secrets-handling surface (API key), multiple new screens and states, and full OOP deliverables graded against a rubric. Single repository, but Level 3's own signals fire: schema/data model work, an API contract consumed across layers, and auth-adjacent user-account data. Full ranked backlog, phased plan, contract-before-consumer, security phase, regression sweep, and CI gate all apply.

## API Decision Record

Scoped decision: **where CodeQuest gets its quiz content, and how the Gemini API key is handled** — for the student submitting this final exam project.

Routes compared (evidence: `docs/codequest-proposal.md:13-15` commits to Gemini REST; Open Trivia DB verified from its official API config page, fetched 2026-11-01; Gemini `generateContent` shape verified from Google's own `generativelanguage v1beta` discovery doc, fetched 2026-11-01):

| Route | Approach | Evidence-based benefit | Evidence-based cost |
|---|---|---|---|
| **A** | User-provided free Gemini key (Google AI Studio), as proposed | Matches the approved proposal; AI-by-topic/difficulty identity; REST+JSON+HttpClient is itself rubric-relevant integration work | Key handling risk; shape drift; demo depends on internet + quota; free tier today is `unverified` (ai.google.dev unreachable) |
| **B** | Free public quiz APIs (Open Trivia DB) | Keyless, free, trivially simple | Verified limitation: generic trivia categories only — closest is "Science: Computers"; no per-language/cybersecurity topic depth; max 50 Q/call; 5 s IP rate limit. Breaks the proposal's topic model |
| **C** (selected) | **Gemini primary + OfflineQuizProvider fallback, both behind one `IQuizProvider` interface** | Everything in A, plus: demo survives no-internet/quota failure; converts red-team risk into a graded OOP demonstration (abstraction + polymorphism: two provider implementations) | Slightly more scope than A — mitigated: the offline provider reads the quizzes the schema/seed phase already produces; the interface is small |
| Do nothing | Static seeded question bank only, no AI | Least work | Contradicts the approved proposal; weakens the Functionality story |

Selection criteria: rubric fit (OOP 20 pts, functionality 25 pts), proposal fidelity, demo-failure risk, incremental effort.

**Decision: Route C.** The app depends on Gemini only for *new* quiz generation; quiz-taking, scoring, history, reports, and CRUD all run fully offline against SQLite.

Red-team: strongest case against C is added scope on a deadline-bound exam project. Mitigation: offline provider = thin reader over seeded DB quizzes (produced anyway in P1); `IQuizProvider` is ~20 lines; if time pressure bites, Route C degrades gracefully to A without redesign (skip the fallback impl, keep the interface). If the user cannot obtain any Gemini key, the interface accommodates a public-API or seeded-only provider without touching consumers. Confidence: **high**. Would flip: evidence that the free tier no longer exists AND the user refuses any paid usage — then Route B/do-nothing hybrid.

### API key handling (the user-facing instruction)

- **The key lives in `config/config.local.json`** (exact path created in P0). That file is **git-ignored from the very first commit** (P0 `.gitignore`); a checked-in template `config/config.example.json` documents the shape without the secret.
- The app reads it at startup via a small config loader; the key is **never hard-coded, never logged, never committed** (verified again in P15 security review, including a git-history scan).
- **When the user must insert the key: between P4 (data access) and P5 (Gemini services).** P0–P4 need no key. P5's verification (mocked-HTTP tests + one live smoke call) is the first thing that needs it. Relay to user: *nothing to do until phase P5 starts; then create a free key at Google AI Studio and paste it into `config/config.local.json` following `config/config.example.json`.*

## Findings Backlog (ranked, Level 3)

Gaps, risks, and contradictions found in `docs/oop-final-exam-guidelines.md` and `docs/codequest-proposal.md` (all `file:line` read this session), ranked by **score = impact × confidence ÷ effort** (1–5 each; effort 5 = largest). The phase(s) resolving each finding and the responsible agent are named; all are scheduled in this plan.

| # | Finding (evidence) | Imp | Conf | Eff | Score | Resolved in | Owner |
|---|---|---|---|---|---|---|---|
| 1 | **Answer-key review vs auto-scoring contradiction**: proposal line 23 requires AI answer keys be checked *before* a quiz is available to students, but line 51 advertises "Automatic scoring" with no review step or status field anywhere | 5 | 5 | 3 | 8.3 | P2 (status workflow in contract), P13 (review/approve screen) | contractor / frontend |
| 2 | **.NET 8 and .NET 9 end support Nov 10, 2026** (Microsoft support policy, updated 2026-10-25); a default ".NET 8 LTS" scaffold choice would target an EOL runtime in 9 days. .NET 10 LTS runs to Nov 14, 2028 | 5 | 5 | 5 | 5.0 | P0 | backend |
| 3 | **Demo internet dependency**: live Gemini call at the exam demo can fail (quota, wifi). Proposal line 23 says offline works for saved quizzes — make offline the rehearsed demo path and AI generation the bonus | 5 | 4 | 2 | 10.0 | P5 (fallback provider), P18 (offline demo script) | middle-end / e2e-tester |
| 4 | **Gemini facts unverified**: free-tier existence, key transport (`x-goog-api-key` header vs `?key=` query), current model name — ai.google.dev unreachable this session. Contract phase must re-verify against live docs before any consumer code | 4 | 4 | 2 | 8.0 | P2 | contractor |
| 5 | **User accounts in schema but no login feature listed**: proposal line 19 stores "user accounts" yet the features table (lines 50-54) has no login; a dead table weakens 3NF scrutiny (rubric 20 pts) and instructor Q&A | 3 | 5 | 2 | 7.5 | P1 (schema), P7 (minimal login) | backend / frontend |
| 6 | **WinForms vs browser e2e tooling**: `e2e-testing`/`playwright-cli` are web-oriented; e2e discipline must adapt to scripted desktop checklists + unit tests (see Verification Adaptation) | 3 | 5 | 2 | 7.5 | P7 onward | e2e-tester |
| 7 | **Explainability constraint**: guidelines lines 170-176 — instructor code review; students must explain their own code. Over-engineering (DI containers, EF Core, clever patterns) is a graded liability. Decision: plain ADO.NET over Microsoft.Data.Sqlite (proposal line 39), small repository layer, no ORM | 4 | 4 | 1 | 16.0 | All phases | all implementers |
| 8 | **CRUD/search/filter is 10 of 25 functionality pts** (guidelines lines 88-91) — the DataGridView management module deserves first-class phase treatment, not an afterthought | 4 | 5 | 2 | 10.0 | P8 | frontend |
| 9 | **Sample records are a named deliverable** (guidelines line 147) — seed data must ship as a committed SQL script, not ad-hoc DB state | 3 | 5 | 2 | 7.5 | P1 | backend |
| 10 | **iOS-style UI in WinForms is non-trivial** (manual custom colors/panels/buttons; default-styled forms read as template work — rubric UI 10 pts). Design-of-record phase must precede all UI phases; polish/unslop pass before demo | 4 | 4 | 3 | 5.3 | P6, P16 | designer / frontend |
| 11 | **Proposal feature table is a `[TABLE]` placeholder** (line 26): exact feature list must be confirmed from prose before design | 2 | 5 | 1 | 10.0 | P6 | designer |
| 12 | **Testing is only 5 rubric pts** (guidelines line 77): scope testing to xUnit unit tests for scoring/validation + scripted checklists; no test pyramid | 2 | 4 | 1 | 8.0 | P3, P14, P18 | middle-end |
| 13 | **SQLite FK enforcement is off by default** — `PRAGMA foreign_keys = ON` must run on every connection or referential integrity (rubric 3 pts) is not actually enforced | 4 | 4 | 1 | 16.0 | P1 (doc), P4 (connection factory) | middle-end |

Findings 7, 11, 13 are effectively standing constraints now encoded in phase acceptance criteria rather than future work; they stay listed so the ranking is recomputable and auditable.

## Verification Adaptation for WinForms

The `e2e-testing` skill and `playwright-cli` are browser tools; CodeQuest is a Windows Forms desktop app, so browser automation does not apply. Adaptation (deliberate, recorded): **e2e-tester verifies desktop phases by executing a scripted functional checklist** — launch the built EXE, drive each documented user path by hand per the checklist, capture screenshots as visual evidence, and confirm console/log health — plus **xUnit unit tests** for pure logic (scoring, validation, JSON mapping). Locator/assertion discipline from `e2e-testing` carries over as: assert on observable app state (records in DB, on-screen values, file effects), not on implementation details. Flakiness rule: a checklist step that cannot be reproduced twice is a defect, not a flake. This is the honest desktop equivalent of a user-path e2e run.

Sizing note: this is one repository containing three layers. Routing within it — WinForms UI code → `frontend` agents; services/business/data-access C# → `middle-end` agents; SQL scripts, solution/CI scaffolding → `backend` agents.

## Phase Index

Dependency order; contract (P2) before consumer (P5, P13); design of record (P6) before any UI implementation (P7+). Sizes: light/heavy; checks: cheap/expensive; e2e/UX per the Level 3 rules.

| Phase | Deliverable | Agent | Size (reason) | Check (reason) | e2e | UX review |
|---|---|---|---|---|---|---|
| P0 | Git repo, solution scaffold, CI | backend | heavy — new project + dependency + build step | cheap — build-only, no behavior | no | none |
| P1 | Schema DDL + seed + 3NF doc | backend | heavy — schema/data model | expensive — data model | deferred (no user path) | none |
| P2 | `contracts/gemini-quiz-api.md` | contractor | light — single artifact, no behavior | cheap — non-functional artifact | no | none |
| P3 | Domain layer (OOP core) | middle-end | heavy — data model + many files | expensive — data model | deferred (no user path) | none |
| P4 | Data access layer | middle-end | heavy — data access, FK/PRAGMA, parameterization | expensive — data | deferred (no user path) | none |
| P5 | Gemini + offline provider services | middle-end | heavy — new external API + error handling | expensive — external API behavior | deferred (service-level unit tests; user path arrives P13) | none |
| P6 | Design spec (design of record) | designer | light — single doc, no behavior | cheap — non-functional artifact | no | none (design phase itself) |
| P7 | Login + shell + navigation | frontend | heavy — new screens | expensive — new state/screen | yes | yes |
| P8 | Management CRUD + search/filter | frontend | heavy — multiple screens, CRUD behavior | expensive — CRUD/data behavior | yes | yes |
| P9 | Quiz taking + scoring | frontend | heavy — new flow + scoring behavior | expensive — behavior | yes | yes |
| P10 | History + answer review | frontend | heavy — new screens + queries | expensive — behavior | yes | yes |
| P11 | Report data services | middle-end | heavy — aggregate queries, new service | expensive — data behavior | deferred (consumed by P12) | none |
| P12 | Reports UI | frontend | heavy — new screen | expensive — behavior | yes | yes |
| P13 | AI generation + review/approve UI | frontend | heavy — new screens, consumes contract | expensive — external API + state change | yes | yes |
| P14 | Validation & error-handling sweep | middle-end | heavy — spans many files | expensive — validation/error behavior | yes (invalid-input paths) | yes (feedback messages) |
| P15 | Security review | security | heavy — secrets, data, threat model | expensive — security posture | no | none |
| P16 | UI polish & unslop pass | frontend | heavy — spans all screens | cheap — visual styling only, no behavior | no (screenshot evidence) | yes (final pass) |
| P17 | ERD, normalization, OOP docs | backend-light | light — docs, no behavior | cheap — non-functional | no | none |
| P18 | Demo-readiness & regression sweep | e2e-tester | heavy — full state matrix | expensive — full behavior | yes (this is it) | none |

"Deferred" e2e = the phase has no user-executable path yet (no UI exists); verification there is build + unit tests + Reviewer. The first e2e run is P7; P18 is the full regression sweep. This is a recorded scoping decision, not an omission: e2e-tester cannot prove a user path that does not exist.

## Phase P0 — Repo & Solution Setup

- **Agent:** backend. **Depends on:** nothing.
- **Files (creates):** `.git/` (git init); `.gitignore` (bin/, obj/, .vs/, `*.db`, `*.db-journal`, `config/config.local.json`, user files); `CodeQuest.sln`; `src/CodeQuest/CodeQuest.csproj` (WinForms, `net10.0-windows`, `Microsoft.Data.Sqlite 10.0.14` per NuGet, fetched 2026-11-01); layer folders `Models/ Services/ Data/ Api/ UI/`; `config/config.example.json` + git-ignored `config/config.local.json` (placeholder only — no real key yet); `tests/CodeQuest.Tests/` (xUnit); `.github/workflows/ci.yml` (windows-latest, `dotnet build` + `dotnet test`); `contracts/` and `plans/` dirs exist.
- **Size: heavy** — new project, new dependency, new build step (any one signal suffices). **Check: cheap** — scaffolding has no runtime behavior; verified by clean build + tests green.
- **e2e:** no. **UX review:** none. **Reviewer web searches:** confirm `Microsoft.Data.Sqlite` latest stable on NuGet and that `net10.0-windows` is a supported target (findings #2).
- **Acceptance criteria:** `git init` done and first commit contains `.gitignore` before any other content; `config/config.local.json` is matched by `.gitignore` (checked with `git status --porcelain` showing it absent / `git check-ignore` positive); `dotnet build` succeeds from a clean clone on .NET 10; `dotnet test` runs the empty test project green; CI workflow file present and triggers on push.
- **Version note:** repository starts at `0.1.0` (see Version Bump Decision).

## Phase P1 — Database Schema, Seed Data & 3NF Documentation

- **Agent:** backend. **Depends on:** P0.
- **Files (creates):** `database/schema.sql` (users, topics, quizzes, questions, choices, quiz_attempts, submitted_answers — 7 interconnected tables, PKs, FKs, ON DELETE actions); `database/seed.sql` (sample records for every table, per deliverable "sample records for testing", guidelines line 147); `docs/normalization.md` (1NF→2NF→3NF walkthrough of every table); `docs/erd.md` (ERD sketch — Mermaid — finalized in P17).
- **Schema facts to honor:** `quizzes.status` CHECK in (`Draft`,`Approved`); `questions` FK → quizzes; `choices` FK → questions with `is_correct`; `quiz_attempts` FK → quizzes + users; `submitted_answers` FK → attempts + questions + chosen choice. Indexes on every child FK column (sqlite.org/foreignkeys.html §3, read 2026-11-01).
- **Size: heavy** — schema/data-model work. **Check: expensive** — data model (referential integrity is 3 rubric pts).
- **e2e:** deferred — no user path; verified by executing scripts. **UX review:** none. **Reviewer web searches:** confirm FK-default-off claim against sqlite.org (verified this session: sqlite.org/foreignkeys.html §2 — FKs disabled by default per connection; also drives P4 acceptance below).
- **Acceptance criteria:** running `schema.sql` then `seed.sql` against a fresh SQLite DB succeeds; an INSERT into `questions` with a nonexistent `quiz_id` **fails** when `PRAGMA foreign_keys=ON` and succeeds when OFF (documenting the P4 requirement); every table has an explicit PK; ≥3 FK relationships exist and are listed in `docs/normalization.md`; the 3NF doc names the functional dependencies eliminated at each step; `quizzes.status` rejects any value outside Draft/Approved.

## Phase P2 — Gemini Integration Contract (contract-first)

- **Agent:** contractor. **Depends on:** P0 (repo exists); **must complete before** P5 and P13 (consumers).
- **Files (creates):** `contracts/gemini-quiz-api.md`. **Reads (verified this session):** Google `generativelanguage v1beta` discovery doc — `POST v1beta/models/{model}:generateContent`, path param `models/{model}`, request `GenerateContentRequest`, response `GenerateContentResponse` with `candidates[].content.parts[].text`, `finishReason`, `promptFeedback.blockReason` (fetched 2026-11-01).
- **Contract contents:** exact endpoint + auth transport; request JSON shape (prompt structure demanding strict JSON quiz output: question text, 4 choices, correct index, explanation, difficulty, topic); response→DTO mapping rules; error shapes (HTTP error, `blockReason` safety block, malformed JSON, timeout) and the app-side `QuizGenerationResult` (success / failure with reason) the app consumers code against; **quiz status workflow** `Draft → Approved` (resolves backlog #1: AI quizzes are born Draft, only Approved quizzes are takeable — proposal line 23); model name + `unverified` items to confirm (free tier, key header vs query param).
- **Size: light** — single artifact, no behavior; routed to `contractor` (sole contract author per roster). **Check: cheap** — non-functional artifact; Reviewer verifies completeness and fidelity to the discovery doc.
- **e2e:** no. **UX review:** none. **Reviewer web searches (mandatory):** fetch live Gemini docs (ai.google.dev or the discovery endpoint used this session) to close the three `unverified` items — free-tier existence/limits, API-key transport, current recommended model name — and update the contract before P5 starts.
- **Acceptance criteria:** contract file exists under `contracts/` and states endpoint, auth, request/response JSON, DTO mapping, four error classes, and the Draft→Approved workflow; the three `unverified` items are either confirmed with a dated source or explicitly recorded as unresolved with a fallback (offline provider) named; P5's delegation may not be issued until this file is complete — no consumer phase may begin against a nonexistent contract.

## Phase P3 — Core OOP Domain Layer

- **Agent:** middle-end. **Depends on:** P1 (schema defines the data model).
- **Files (creates under `src/CodeQuest/`):** `Models/UserAccount.cs`, `Models/Topic.cs`, `Models/Quiz.cs`, `Models/Question.cs` (abstract base), `Models/MultipleChoiceQuestion.cs`, `Models/TrueFalseQuestion.cs` (derived), `Models/Choice.cs`, `Models/QuizAttempt.cs`, `Models/SubmittedAnswer.cs`; `Models/Difficulty.cs`, `Models/QuizStatus.cs` (enums matching P1 CHECK constraints); `Services/IQuizProvider.cs` (abstraction — P5 adds two implementations), `Services/IScoringService.cs`, `Services/ScoringService.cs`, `Data/IQuizRepository.cs` (+ repository interfaces per entity, consumed by P4); **tests:** `tests/CodeQuest.Tests/ScoringServiceTests.cs`.
- **Rubric mapping (guidelines lines 36-41, 104-108):** encapsulation = private fields + public properties on every model; inheritance = `Question` base → two derived classes; polymorphism = overridden behavior (e.g., `ValidateAnswer()` / display formatting) across derived classes; abstraction = `IQuizProvider`, `IScoringService`, repository interfaces. These four must be pointable in code review — implementers add a short comment header per concept so the instructor walkthrough is easy (guidelines line 170: students must explain their own code).
- **Size: heavy** — data model + many files. **Check: expensive** — data model. **e2e:** deferred (no user path). **UX review:** none. **Reviewer web searches:** none — internal facts.
- **Acceptance criteria:** project builds; `ScoringServiceTests` covers: all-correct, mixed, none-correct, empty-attempt (≥4 cases, each independently checkable); `MultipleChoiceQuestion` and `TrueFalseQuestion` both compile as `Question` subclasses and override at least one member; every model has private fields with public properties (grep-able); no model references SQLite or HttpClient (separation of concerns — rubric line 119).

## Phase P4 — Data Access Layer

- **Agent:** middle-end. **Depends on:** P1 (schema), P3 (interfaces).
- **Files (creates under `src/CodeQuest/`):** `Data/Db.cs` (connection factory: opens `SqliteConnection`, runs `PRAGMA foreign_keys = ON` on **every** open — verified requirement, sqlite.org/foreignkeys.html §2; loads DB path from config), `Data/Repositories/TopicRepository.cs`, `QuizRepository.cs`, `QuestionRepository.cs`, `AttemptRepository.cs`, `UserRepository.cs` — each implementing its P3 interface; **tests:** `tests/CodeQuest.Tests/RepositoryTests.cs` (against a temp DB built from `database/schema.sql` + `database/seed.sql`).
- **Constraints honored:** every query parameterized (`command.Parameters.AddWithValue("$name", …)` per Microsoft Learn overview, read 2026-11-01) — zero string interpolation into SQL; transactions for multi-row writes (quiz + questions + choices; attempt + answers).
- **Size: heavy** — data access + FK/PRAGMA + parameterization across repositories. **Check: expensive** — data behavior. **e2e:** deferred (no user path). **UX review:** none. **Reviewer web searches:** none — external facts already verified this session (Microsoft Learn, sqlite.org).
- **Acceptance criteria:** repository tests pass against a seeded temp DB; a grep for string-concatenated SQL in `Data/` returns nothing (all SQL is constant text + parameters); deleting a topic that has quizzes **throws** (FK enforced, proving PRAGMA is on); a quiz+questions+choices write is atomic (transaction test: forced failure leaves no partial rows); CRUD exists for topics, quizzes, questions/choices, attempts.

## Phase P5 — Gemini Quiz Generation Services

- **Agent:** middle-end. **Depends on:** P2 (contract — hard gate), P4 (repositories). **⚠ USER KEY REQUIRED HERE** (see API Decision Record — insert key into `config/config.local.json` before this phase's verification).
- **Files (creates under `src/CodeQuest/`):** `Api/GeminiClient.cs` (HttpClient wrapper per contract: endpoint, auth transport per verified contract, timeouts, error mapping to the contract's four error classes); `Api/GeminiDtos.cs` (request/response DTOs per contract); `Services/GeminiQuizProvider.cs` (implements `IQuizProvider`: builds prompt from topic/difficulty/count, parses strict-JSON answer, maps to `Quiz` with `QuizStatus.Draft`); `Services/OfflineQuizProvider.cs` (implements `IQuizProvider`: returns seeded/approved quizzes from DB — the fallback); `Services/QuizGenerationService.cs` (orchestrator: try Gemini → on failure report reason, never crash); `Services/AppConfig.cs` (reads `config/config.local.json` once; key never logged); **tests:** `tests/CodeQuest.Tests/GeminiProviderTests.cs` (mocked `HttpMessageHandler`: valid response, malformed JSON, HTTP 429/500, safety-block `blockReason`, timeout).
- **Size: heavy** — new external API + error-handling design. **Check: expensive** — external API behavior. **e2e:** deferred — user path arrives at P13; verification here is mocked unit tests + one **optional** live smoke call (documented output in the phase report). **UX review:** none. **Reviewer web searches:** re-verify endpoint/model/auth against live docs (same searches as P2) before PASS.
- **Acceptance criteria:** mocked tests cover all four contract error classes and pass; a valid mocked response produces a `Quiz` object with questions, choices, correct answers, explanations, and `Status == Draft`; with no network key configured, `QuizGenerationService` returns a typed failure (not an exception); the API key string appears nowhere in source, logs, or test output (grep); one live smoke call (if key present) returns a parseable quiz and its raw JSON is saved to the phase report for the contract's mapping section.

## Phase P6 — iOS-Style Design Spec (Design of Record)

- **Agent:** designer. **Depends on:** P0 (repo); **must complete before** P7 (first UI phase) — design of record precedes frontend work.
- **Files (creates):** `designs/codequest-ui-spec.md` — full visual system: color tokens (light neutral surfaces, one accent), typography scale, spacing scale, control restyle recipes for WinForms (custom flat buttons, rounded panels, DataGridView restyle, message-box conventions), screen-by-screen layouts for every P7–P13 form (login, shell/nav, management, quiz-taking, history, reports, generation, review), empty/loading/error states, and the feature list confirmed from proposal prose (backlog #11 — the `[TABLE 1]` placeholder at proposal line 26 is not a feature list; the designer confirms features from lines 7-9 and 50-54 before designing).
- **Reference discipline:** implementers apply the `ui-reference` skill (search best-in-class iOS-style references, extract layout/spacing/type/color patterns). The spec names the reference patterns it derives from.
- **Size: light** — single doc, no behavior. **Check: cheap** — non-functional artifact. **e2e:** no. **UX review:** none (this phase *produces* the design; `ux-reviewer` judges implemented UI in P7+). **Reviewer web searches:** none mandatory; designer's own `ui-reference` searches are the evidence base.
- **Acceptance criteria:** spec exists and covers every screen listed in P7–P13; every WinForms control type used later has a stated restyle recipe; spec is in iOS-clean territory (no purple gradients, no glows, no gradient text, no emoji labels, no uppercase tracked labels — the `unslop` sign list is the checklist); feature list section resolves backlog #11 explicitly.

## Phase P7 — UI Shell & Navigation (Login + Main Window)

- **Agent:** frontend. **Depends on:** P4 (user repository for login), P6 (design spec).
- **Files (creates under `src/CodeQuest/UI/`):** `LoginForm.cs` (validates against `users` table via `UserRepository` — resolves backlog #5: the users table gets a purpose; single seeded user documented in seed data), `MainForm.cs` (sidebar/navigation shell per spec), `UITheme.cs` (spec tokens as constants), placeholder panels routing to P8–P13 modules. Edits `Program.cs` (bootstrap order: config → theme → login → shell).
- **Size: heavy** — new screens/state. **Check: expensive** — new screens + navigation state. **e2e:** **yes** — first runnable user path (scripted checklist: launch → wrong password rejected with message → correct login → shell → each nav target opens → logout). **UX review:** **yes** — runs before the Reviewer gate; verified findings feed the Reviewer's PASS/FAIL. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** wrong password shows a non-blocking error and stays on login; correct login opens the shell; every nav entry opens its module panel and returns without data loss; app follows the P6 spec (tokens in `UITheme.cs`, no default-styled chrome on these screens); wrong-DB-missing-file startup shows a friendly message, not a stack trace; e2e checklist + screenshots recorded; ux-reviewer findings addressed or explicitly dispositioned.

## Phase P8 — Topic & Quiz Management (CRUD, Search, Filter)

- **Agent:** frontend. **Depends on:** P4 (repositories), P6 (spec), P7 (shell to host panels).
- **Files (creates under `src/CodeQuest/UI/`):** `TopicManageForm.cs`, `QuizManageForm.cs`, `QuestionEditForm.cs` (questions + choices editing, correct-answer selection) — DataGridView-based CRUD with search box + topic/status filters, delete-confirmation dialogs, validation messages; edits shell wiring. This phase is the rubric's biggest block: CRUD 10 pts + search/filter/report share (guidelines lines 88-91).
- **Size: heavy** — multiple screens, CRUD behavior. **Check: expensive** — CRUD/data behavior. **e2e:** **yes** (scripted: create topic → create quiz + questions + choices → edit → search by name → filter by status/topic → delete → confirm FK-protected delete shows a friendly message instead of raw SQLite error). **UX review:** **yes**. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** full CRUD works for topics, quizzes, and questions-with-choices through the UI; search filters the grid live; status/topic filter combos work; invalid input (empty name, duplicate topic, quiz with zero choices on a question) is rejected with a specific message; deleting a parent with children is blocked gracefully (FK handled, message shown); grid restyle matches P6 spec; e2e checklist + screenshots recorded.

## Phase P9 — Quiz Taking & Auto Scoring

- **Agent:** frontend. **Depends on:** P3 (`ScoringService`), P4 (`AttemptRepository`), P6, P7.
- **Files (creates under `src/CodeQuest/UI/`):** `QuizTakeForm.cs` (choose from **Approved** quizzes only — Draft quizzes are listed disabled with a "pending review" tag, enforcing proposal line 23 at the UI level), question-by-question navigation (next/previous, progress indicator), submit → `ScoringService` → save `QuizAttempt` + `SubmittedAnswer` rows via `AttemptRepository`, result summary screen (score, per-question correctness); edits shell wiring.
- **Size: heavy** — new flow + scoring behavior. **Check: expensive** — behavior. **e2e:** **yes** (scripted: take seeded quiz → answer all → submit → score matches `ScoringService` unit-test math → attempt row exists → retake works; plus: Draft quiz is not takeable). **UX review:** **yes**. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** only Approved quizzes are takeable (a Draft quiz cannot be started from the UI); answers persist per question; score = correct/total, shown as number and percentage; submitting twice cannot create duplicate attempts (guard); leaving mid-quiz via nav does not crash and discards the attempt cleanly (documented behavior); result summary lists each question with chosen vs correct; e2e checklist + screenshots recorded.

## Phase P10 — Quiz History & Answer Review

- **Agent:** frontend. **Depends on:** P4, P6, P7, P9 (attempts exist).
- **Files (creates under `src/CodeQuest/UI/`):** `HistoryForm.cs` (attempt list — DataGridView of past attempts: date, quiz, topic, score; searchable by quiz name, filterable by topic/score) and `AttemptReviewForm.cs` (drill into one attempt: each question, the answer submitted, correct/incorrect, explanation text — full offline access per proposal line 9).
- **Size: heavy** — new screens + queries. **Check: expensive** — behavior. **e2e:** **yes** (scripted: complete a quiz → it appears in history → filter/search → open review → every submitted answer visible with correct/incorrect marks). **UX review:** **yes**. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** every completed attempt appears with date, quiz, topic, score; search and filters narrow the grid; review view shows chosen answer vs correct answer and the explanation for every question; history works with the network cable unplugged (offline requirement, proposal line 23); e2e checklist + screenshots recorded.

## Phase P11 — Report Data Services

- **Agent:** middle-end. **Depends on:** P4; **before** P12 (its only consumer).
- **Files (creates under `src/CodeQuest/`):** `Services/IReportService.cs`, `Services/ReportService.cs` — parameterized aggregate queries: attempts per topic (avg/best/latest score), per-difficulty performance, topics-need-practice list (avg score below threshold), overall summary (total attempts, total quizzes taken, overall average); **tests:** `tests/CodeQuest.Tests/ReportServiceTests.cs` against seeded temp DB.
- **Size: heavy** — new service + aggregate query design. **Check: expensive** — data behavior. **e2e:** deferred — consumed by P12, which carries the user-path run. **UX review:** none. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** each aggregate returns correct values against the seed data (asserted in unit tests with hand-computed expected numbers); queries are parameterized (any topic filter is a parameter, never concatenation); a topic with zero attempts returns a defined empty result, not an exception; interface allows P12 to be developed and reviewed against `IReportService` alone.

## Phase P12 — Performance Reports UI

- **Agent:** frontend. **Depends on:** P11 (service interface + implementation), P6, P7.
- **Files (creates under `src/CodeQuest/UI/`):** `ReportsForm.cs` — summary panel (totals + overall average) and per-topic DataGridView (topic, attempts, avg %, best %, weak-topic flag), topic/difficulty filter; "needs practice" list sorted ascending by average; edits shell wiring. Fulfills the ≥1 report/summary requirement (guidelines line 51).
- **Size: heavy** — new screen. **Check: expensive** — behavior. **e2e:** **yes** (scripted: complete several seeded quizzes → open reports → numbers match the attempts actually made → filter by topic → weak-topic list ranks lowest first). **UX review:** **yes**. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** report numbers match the `ReportServiceTests` expectations for the same seed data (UI and service agree); empty history (no attempts) shows a friendly empty state, not zeros presented as data; weak-topic list is sorted and clearly labeled; e2e checklist + screenshots recorded.

## Phase P13 — AI Generation & Answer-Key Review UI

- **Agent:** frontend. **Depends on:** P5 (providers — consumer of an existing contract), P4, P6, P7, P8 (management forms to reuse quiz grids).
- **Files (creates under `src/CodeQuest/UI/`):** `GenerateQuizForm.cs` (topic picker, difficulty Easy/Medium/Hard, question count 5–20, Generate button → calls `QuizGenerationService`; busy indicator during the call; typed-failure banner with the contract's reason when offline/quota fails — pointing the user at the offline path); `QuizReviewForm.cs` (lists `Draft` quizzes → opens one → shows each AI question, choices, **the AI's claimed correct answer and explanation** → reviewer approves (status → `Approved`, then takeable in P9) or edits/corrects or discards); edits `QuizManageForm.cs` to surface Draft/Approved status badges.
- **This phase resolves backlog #1** — the proposal's review-before-availability note (line 23) becomes a real, demonstrable screen; excellent presentation material (rubric 10 pts).
- **Size: heavy** — new screens, consumes external API, state change (Draft→Approved). **Check: expensive** — external API + state change. **e2e:** **yes** (scripted, two paths: **online** — generate live quiz → lands as Draft → review → approve → appears takeable; **offline** — kill network → generate shows a clear typed failure, app stays usable → seeded offline quiz still fully takeable). **UX review:** **yes**. **Reviewer web searches:** re-verify Gemini endpoint/model/auth unchanged (same searches as P2/P5) before PASS.
- **Acceptance criteria:** generation produces a Draft quiz whose questions/choices/explanations render in the review screen; approval flips status and the quiz becomes takeable (verify in P9 flow); a Draft quiz is never startable; offline/quota failure shows the typed failure message and never crashes or leaves a half-saved quiz; e2e checklist + screenshots recorded for both paths.

## Phase P14 — Validation & Error-Handling Sweep

- **Agent:** middle-end. **Depends on:** P7–P13 existing (sweep across them).
- **Files (edits across `src/CodeQuest/UI/` and `Services/`):** consistent validation helpers (one shared validator, applied to every input form), consistent exception handling (no silent catch; user-facing messages per rubric line 56: appropriate messages for successful *and* unsuccessful operations), success feedback (subtle toast/status text per spec) on every create/update/delete, logging hook (file-based, no key material) for unexpected exceptions.
- **Size: heavy** — spans many files. **Check: expensive** — validation/error-handling behavior (2 rubric pts + feeds UX 3 pts). **e2e:** **yes** — invalid-input paths on every form (empty, overflow, duplicate, FK-blocked delete). **UX review:** **yes** — message quality and feedback consistency. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** every text input has declared validation (required, length, numeric range as applicable) and rejects bad input with a specific message; every CRUD action shows success feedback; no form can be crashed from the keyboard in a 5-minute adversarial pass (recorded); unexpected exceptions surface a friendly dialog + log line, never a raw stack trace to the user; grep finds no empty catch blocks.

## Phase P15 — Security Review

- **Agent:** security. **Depends on:** P5 (key handling exists), P14 (validation in place).
- **Scope (compact threat model for a local desktop app):** secret handling — key present only in git-ignored `config/config.local.json`, never in source/logs/exceptions, git history clean (`git log -p` scan for the key pattern since P0); SQL injection — all queries parameterized (audit `Data/` — rubric line 35); FK/referential integrity enforced; input validation coverage; no PII beyond a display name (data-minimization note for documentation); exception messages never leak connection strings.
- **Files (creates):** `docs/security-notes.md` (threat model, findings, dispositions). **Size: heavy** — secrets + data + threat model. **Check: expensive** — security posture. **e2e:** no. **UX review:** none. **Reviewer web searches:** confirm API-key handling guidance against current vendor docs (closes the key-transport `unverified` item with an implementation-adjacent source).
- **Acceptance criteria:** `git log -p | grep` for the key format finds nothing across all history; every SQL statement in `Data/` is constant text + parameters (audit list attached); the key is not present in any log file produced by running the app; findings are each dispositioned (fixed / accepted-with-reason) in `docs/security-notes.md`; no HIGH finding remains open at PASS time.

## Phase P16 — UI Polish & Unslop Pass

- **Agent:** frontend. **Depends on:** P7–P13 screens complete + ux-reviewer findings accumulated from those phases.
- **Five-line direction (derived from the product — an iOS-clean student quiz app; the agent runs the `unslop` passes against it):**
  1. Calm light-neutral surfaces with one accent color; even 8px-scale spacing everywhere — no glossy blobs, no decorative gradients or glows.
  2. Segoe UI with a clear 3-step size hierarchy; no gradient text, no uppercase tracked labels.
  3. Plain-language copy ("Save", "Try again", "No quizzes yet") — no emoji labels, no shouty text, consistent empty-state wording.
  4. DataGridViews restyled as iOS-like lists: hairline row separators, quiet headers, no default blue selection chrome.
  5. Stillness over fake motion — no looping animation hacks; subtle state changes only where WinForms supports them cleanly.
- **Files (edits across `src/CodeQuest/UI/` + `UITheme.cs`):** whatever the audit surfaces; **no behavior changes**.
- **Size: heavy** — spans all screens (if the audit finds only a few named signs, downgrade to `frontend-light` with the same scope note). **Check: cheap** — visual styling only; behavior must be untouched (any behavior change found gets reclassified and retroactively tested per the standing rule). **e2e:** no — screenshot evidence before/after per screen instead. **UX review:** **yes** — this pass consumes it. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** every screen passes the `unslop` sign checklist (each named sign either absent or fixed); before/after screenshots recorded for every touched form; app behavior identical (spot-check the P9 take-quiz path still passes); ux-reviewer's accumulated findings each fixed or dispositioned.

## Phase P17 — Documentation Deliverables

- **Agent:** backend-light. **Depends on:** P1 (schema), P3 (OOP structures), P15 (security notes exist); before P18.
- **Files (creates/edits):** `docs/erd.md` (final ERD with crow's-foot relationships matching deployed schema); `docs/normalization.md` (finalize P1 draft — every table through 1NF/2NF/3NF with the dependencies removed at each step); `docs/oop-explanation.md` (each of the four concepts with class names + file paths from P3); `docs/system-description.md` (title, objectives, features/modules per guidelines deliverable list, lines 139-156); `docs/user-guide.md` (short demo walkthrough — doubles as the demo script).
- **Size: light** — documentation only, no behavior. **Check: cheap** — non-functional. **e2e:** no. **UX review:** none. **Reviewer web searches:** none — all internal facts; every path/named class in the docs is verified against the actual tree before PASS.
- **Acceptance criteria:** all five docs exist; every class, file path, table, and column named in them exists in the repo (Reviewer spot-checks); ERD matches `database/schema.sql` exactly (tables, columns, FKs); OOP doc points at real `file:line` locations for each concept; docs use the app's real feature names.

## Phase P18 — Demo-Readiness Verification & Regression Sweep

- **Agent:** e2e-tester. **Depends on:** all previous phases PASS.
- **Scope:** full scripted state matrix on a **fresh clone**: build → schema+seed → launch → login → CRUD → offline quiz-taking + scoring → history → reports → (online) AI generation → review → approve → take → history shows it → reports reflect it; adversarial invalid-input pass; log health check; then **offline demo rehearsal**: network disconnected, run the exact demo script end-to-end on seeded data (backlog #3).
- **Files (creates):** `docs/demo-script.md` (the exact clicks, in order, with expected outcomes — the student rehearses from this); e2e evidence pack (screenshots per step) in the phase report.
- **Size: heavy** — full state matrix. **Check: expensive** — this is the full-behavior verification. **e2e:** yes (this is the regression sweep). **UX review:** none (final UX pass was P16). **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** every scripted step produces its expected outcome on a clean machine-equivalent run; the offline rehearsal completes with zero failed steps; no step depends on developer-only state (paths, hacks); `docs/demo-script.md` is executable by someone who has never seen the code; any failure reopens the owning phase — the plan stays active until green.

## Version Bump Decision

New project with no prior version: first push (after P0) carries **0.1.0**. Phases during development bump **patch (0.1.x)** for fixes; the demo-ready build after P18 PASS is **1.0.0** (minor→major step is justified: first complete, submission-grade release). No major bump is foreseen before that. This decision is final for pushes; the push agent does not re-decide.

## Assumptions & Open Questions

1. **Single local user** (login validates against a seeded `users` row) — multi-role teacher/student separation is out of scope; the review screen (P13) is a nav item, matching a single-examiner context. If the instructor expects multi-user, this is a P1 schema + P7 change (flagged early so the cost is known).
2. **Demo machine** runs Windows with the .NET 10 SDK or a self-contained publish is prepared — P18's fresh-clone run proves whichever is chosen.
3. **Gemini free tier** is `unverified` (ai.google.dev unreachable this session). The plan does not depend on it: P5's failure path + P18's offline rehearsal cover the worst case. If the contract phase (P2) confirms no free tier, the user decides between a minimal paid tier and Route B/do-nothing — the `IQuizProvider` seam absorbs either without redesign.
4. **Exam date** is unknown to this plan; phase order is dependency-driven, and P8 (CRUD) + P9 (quiz-taking) are deliberately front-loaded after the shell so the highest-rubric-value modules exist early even if time compresses later phases.
5. The proposal's `[TABLE]` placeholders (lines 26, 31, 48) are formatting artifacts; P6 resolves the feature list from prose and the Orchestrator should confirm the summary above matches the user's intent at P6 review time.
