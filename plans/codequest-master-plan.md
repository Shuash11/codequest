# CodeQuest — Master Implementation Plan

## Status

`active` — created 2026-11-01. Adviser is the only writer; all other agents read-only.

## Standing Rules (inherited by every delegation)

Incident 2026-11-01: an implementation agent deleted the user's `.opencode/` folder (user property containing workflow config and a project-specific secret). Restored by hand; formal reprimand issued. The rule below is now binding for every phase; the Orchestrator restates it in every delegation prompt, but this plan owns it.

**Standing Rule 1 — Never touch what you did not create.** An agent may delete, move, rename, or empty only files and folders it created within its own delegated task. `.opencode/` (contains user configuration and secrets — verified git-ignored at `.gitignore:48`, never committed) and all pre-existing repo content (`docs/`, `plans/`, `contracts/`, `designs/`, root files) are off-limits to destructive operations for the entire project.

**Standing Rule 2 — Destructive commands on pre-existing paths are forbidden.** Without explicit, task-specific Adviser approval: `Remove-Item`, `rmdir`/`rd`, `del`/`erase`, `git clean`, `git reset --hard`, `git checkout -- .`, `git restore .`, and any force flag (`-Force`, `-f`, `--force`) applied to anything the agent did not create in its own task.

**Standing Rule 3 — Stop and report instead of improvising.** If a task appears to require removing or altering something the agent did not create, the agent stops and reports the perceived need back to the Orchestrator. Removal is never a side effect of another operation. Violations are formal incidents with consequences.

## Adviser Rulings & Phase Progress

**R-1 (2026-11-01): `docs/` and `plans/` stay tracked in git — commit `8e5a921` approved.** The master plan is by definition a checked-in artifact under `plans/` (a fresh clone must retain it), and P17's graded documentation deliverables live in `docs/` alongside the user's exam documents. `git rm --cached` was rejected: it would make the plan vanish on fresh clone and contradict the checked-in-artifact rule. Constraint for future phases: agents **append** to `docs/` and `plans/`; they never delete or overwrite the user-provided exam documents (`docs/oop-final-exam-guidelines.md`, `docs/codequest-proposal.md`). Re-evaluate this ruling only if the repository is ever made public.

| Date | Phase | Verdict | Push |
|---|---|---|---|
| 2026-11-01 | P0 — Repo & Solution Setup | PASS (Reviewer, no defects; all 5 acceptance criteria individually verified) | Pushed 0.1.0 → private `Shuash11/queziah`, `main` @ `8e5a921` (no squash/amend); CI run 41300422777 on `.github/workflows/ci.yml` **GREEN** (1m19s, Build+Test pass) |
| 2026-11-01 | P1 — Database Schema, Seed Data & 3NF | Implemented → Review FAIL (1 verified low defect: false parenthetical in seed.sql comment; 2 advisories) → fix `ea96a36` → targeted re-review **PASS** (all corrections verified, zero open defects). Commits `9ced7e5` + `ea96a36` + R-2 #2 PBKDF2 amendment `d8a03a3`. Reviewer skipped web-search (internal facts, verified by execution); sqlite.org FK-default-off re-confirmed (§2, updated 2026-10-25) | Pushed 0.1.0 → `3f7a6de..d59d10a main` (4 commits incl. plan docs-only `d59d10a`); CI run 41336289486 **GREEN** (1m24s, Build+Test pass); Node.js 20 annotation recurred (backlog #14) |
| 2026-11-02 | P2 — AI Provider Integration Contract | Authored → Reviewer **PASS** (11/11 acceptance criteria, zero defects). All four mandatory searches run with sources: NVIDIA Bearer auth confirmed (NIM reference updated 2026-10-23 + OpenAPI bearer scheme) — closes the plan's carried `unverified` item; OpenRouter compat/auth confirmed; both live model catalogs checked (slugs real, none pinned); free-tier limits re-confirmed (20 req/min, 50/day <10 credits). Advisories S1–S4 dispositioned in R-3 | Pushed 0.1.0 — `b476aeb` + Adviser plan docs commit `2e395a4` (route (a)) |
| 2026-11-02 | P3 — Core OOP Domain Layer | Implemented → Review FAIL (all 4 acceptance criteria verified green — build, 6/6 tests incl. 5 scoring cases, real inheritance/polymorphism, clean encapsulation/purity sweeps, contract seam verified field-by-field, R-2 #1 honored — but 2 comment-only defects blocked PASS: false "private list" claim in `Choice.cs` header; stale test-count claim) → fix `9121cc2` (comment-only; fixer correctly wrote the real method name `IsCorrectChoice` rather than inventing a false comment) → targeted re-review **PASS** (diff provably comment-only, no regression). Commits `9fe8d8d` + `ceb2cd6` + `9121cc2`. Reviewer search-skip valid (internal facts). CLR type mappings documented in model headers for P4; `.gitkeep` cleanup → R-4; Api/.gitkeep Gemini comment → R-3/B1 | Pushed 0.1.0 — 3 commits to origin/main |
| 2026-11-02 | P4 — Data Access Layer | Interrupted-run recovery done right (P4a adopted verified partial work: 4 adopted, 2 fixed, 5 created) → P4b's new test suite exposed a **real defect**: stored UTC timestamps parsed with a quoted `'Z'` as a literal, shifting reads by −(local offset) on UTC+8 → one-line fix `d91af59` (`AssumeUniversal\|AdjustToUniversal`), suite landed with zero test edits (SHA256-verified byte-identical) → full review **PASS** (5/5 criteria, suite 15/15 re-run twice, zero defects). Three flagged decisions ACCEPTed (question-kind reconstruction; `AddWithAnswers` additive seam for P9; id stamping). Commits `35eb080` + `d91af59` + `da09440`. The UTC arc is demo material for the testing/validation rubric. Low advisory (read-path DB errors) → P14 sweep item | Push approved at 0.1.0 — 3 commits to origin/main |

**R-2 (2026-11-01): P1 schema design decisions ratified — downstream obligations binding on later phases.** These are schema contracts; changing any of them is a schema-change phase through the Adviser.

1. **Derived fields omitted (3NF).** `quiz_attempts` stores only `score`; `submitted_answers` has no `is_correct`. Obligations: **P9** computes per-answer correctness by joining `choices.is_correct` and the score denominator by counting the quiz's questions; **P10** derives correctness live in the review view; **P11** computes all report aggregates from joins — none of these phases may re-add stored derived columns.
2. **Password hashing — AMENDED from the P1 decision.** The stored format becomes **`pbkdf2-sha256:<iterations>:<salt-b64>:<hash-b64>`** — PBKDF2-HMAC-SHA256, **600,000 iterations**, random per-user salt. Basis: OWASP Password Storage Cheat Sheet (fetched 2026-11-01) states fast hashes such as SHA-256 are *not suitable* for password storage and recommends PBKDF2 at 600k iterations (also the NIST SP 800-63B-4 path). Implementation stays dependency-free: .NET's built-in `Rfc2898DeriveBytes` — no new package, keeping standing constraint #7 (explainability). The schema column (`schema.sql:32`, `TEXT NOT NULL`) needs no change; a fix commit updates the column comment and the seeded `demo` row (`seed.sql:44`) to the new format, followed by a targeted one-line factual re-review (the embedded hash must actually verify against `codequest123`). Obligations: **P7** implements a small `PasswordHasher` (hash + verify) with exactly these parameters, plus a unit test proving the seeded value verifies; **P15** records the disposition instead of flagging it.
3. **App-level validations deferred by design.** **P4** must validate that an answered question belongs to the attempt's quiz; **P8** must enforce exactly-one-correct-choice per question; **P9** scores via the joins from decision 1.
4. **No `approved_at`/`approved_by` audit columns.** **P13** flips `status` Draft→Approved only. If audit history is ever wanted, it is a schema-change phase through the Adviser — not an improvisation.
5. **UNIQUE natural keys:** `(topic_id,title)`, `(quiz_id,position)`, `(question_id,choice_text)`. **P8** catches UNIQUE violations and shows friendly duplicate messages (feeds P14's message sweep).
6. **`explanation NOT NULL`.** **P8**'s editor always supplies a value (empty string when none); **P10** and **P13** display it.
7. **`topics.category` CHECK (`Programming`,`Cybersecurity`).** **P8**'s topic form uses a dropdown limited to these two values — never free text. Adding a category = schema-change phase.
8. **`position` columns with UNIQUE per parent.** **P8** keeps positions unique and stable; reordering happens inside a transaction (P4's transaction helper).

**R-3 (2026-11-02): P2 contract edge-default clarifications (binding on P5) + advisory dispositions.** Source: P2 Reviewer advisories S1–S4 (non-blocking; zero defects). These interpretations bind P5's implementation and its Reviewer; the contract file stays as authored — any future contract-file change is a contractor mini-delegation.
- (a) S4-1: transport-level failures (DNS, connection refused/reset) map to the timeout/network-unreachable error class, with the distinguishing detail in the failure reason string — never an unhandled exception.
- (b) S4-2: an absent per-provider `enabled` flag defaults to *enabled*; the skip rule stands regardless — a provider with an empty key is skipped.
- (c) S4-3: HTTP 200 with zero or empty choices is a distinct malformed-response class and triggers chain failover like any other per-provider failure.
- S1 (accepted, folded into P5): model-slug validity is proven by a real generation smoke call, not by NVIDIA's public `/v1/models` listing (which omits documented slugs — vendor surface discrepancy).
- S2 (accepted as documented nuance): P5 keys auth-failure detection on 401/403 status, not message wording.
- S3 (no action): cosmetic taxonomy label in the contract's model table; slug validity is re-proven by P5's smoke test.
- B1 (confirmed; one refinement): P5 refreshes the stale Gemini references in its pass — `config/config.example.json` becomes the two-provider `aiProviders` shape (this *is* the shape change, contract §8), and the `.gitignore:22` comment is updated; the `src/CodeQuest/Api/.gitkeep` comment refresh is **superseded by R-4**: P5 deletes that `.gitkeep` (it now touches `Api/` with real files), which is the better cleanup.

**R-4 (2026-11-02): scaffold-era `.gitkeep` placeholders — cleanup permission granted.** The `.gitkeep` files P0 scaffolded into source folders are inert once the folders contain real files, but they are stray clutter under instructor code review (code-quality rubric). Ruling: the **next phase whose in-scope edits already touch a given folder** may delete that folder's `.gitkeep` as part of its pass — this is the explicit Adviser approval Standing Rule 2 requires. Sequence as phases pass through: P4 → `Data/`, P5 → `Api/` (already owns its refresh), P7 → `UI/`, first `Models/`/`Services/`-touching phase thereafter. Deletion of the file itself only; nothing else in the folder changes.

## Task Level Declaration

**Level 3 (Complex).** Reason: a brand-new application with a relational schema and data model (SQLite, 7 tables), an external AI API integration (NVIDIA NIM / OpenRouter, OpenAI-compatible chat completions via HttpClient — owner directive 2026-11-02 replaced the originally proposed Gemini), a secrets-handling surface (AI provider keys), multiple new screens and states, and full OOP deliverables graded against a rubric. Single repository, but Level 3's own signals fire: schema/data model work, an API contract consumed across layers, and auth-adjacent user-account data. Full ranked backlog, phased plan, contract-before-consumer, security phase, regression sweep, and CI gate all apply.

## API Decision Record

Scoped decision: **where CodeQuest gets its quiz content and how the AI provider keys are handled** — for the student submitting this final exam project. **Owner directive (2026-11-02, final): do NOT use Google Gemini; use NVIDIA's API and/or OpenRouter, with "both" the preferred option.** This overrides `docs/codequest-proposal.md` §3; the exam documents themselves stay untouched (R-1), so this record is the authoritative statement of the override.

Routes compared (evidence fetched 2026-11-02 from vendor-primary sources: NVIDIA NIM LLM API reference — `POST https://integrate.api.nvidia.com/v1/chat/completions`, docs updated 2026-10-23, with a large instruct-model catalog; OpenRouter quickstart — OpenAI-SDK drop-in at base `https://openrouter.ai/api/v1`, `Authorization: Bearer`, and OpenRouter Limits — `:free` model variants capped at 20 req/min and 50 req/day for accounts that have purchased <10 credits, 1000 req/day at ≥10 credits):

| Route | Approach | Evidence-based benefit | Evidence-based cost |
|---|---|---|---|
| **A** | NVIDIA NIM only | Owner key already exists; one endpoint, verified OpenAI chat-completions shape; strong instruct catalog (llama-3.3-70b-instruct, nemotron, qwen3, gpt-oss) | Single point of failure for live generation (quota/network at demo time) |
| **B** | OpenRouter only | Hundreds of models incl. `:free` variants; automatic provider/model fallback routing on their side | Requires new key signup; free daily caps are tight (50/day <10 credits); NVIDIA key unused against owner preference |
| **C** (selected) | **Provider chain: NVIDIA primary → OpenRouter `:free` fallback → OfflineQuizProvider (seeded DB) last resort — one `IQuizProvider` interface; one OpenAI-compatible client parameterized per provider** | Matches the owner's preferred "both"; both verified OpenAI chat-completions-compatible, so ONE client + per-provider config (base URL, key, model) covers all AI backends; demo survives NVIDIA quota loss, OpenRouter caps, and total network loss; still exactly two `IQuizProvider` implementations (AI chain + offline) — the OOP story (abstraction + polymorphism) is unchanged | Two keys to manage; chain-failover is one extra test surface — mitigated: failover is a loop over config, one additional mocked test case |
| Do nothing | Seeded question bank only, no AI | Least work | Contradicts the owner directive's intent and the proposal's AI identity |

Selection criteria: owner-directive fidelity, demo-failure risk, wire-format verification (both are OpenAI-compatible — one contract, not two), incremental effort, explainability.

**Decision: Route C.** The app depends on the AI chain only for *new* quiz generation; quiz-taking, scoring, history, reports, and CRUD all run fully offline against SQLite. A configured provider with an empty key is skipped, so the app runs with NVIDIA only, OpenRouter only, both, or neither (offline-only) — no code path changes.

Red-team: strongest case against C is added scope (two providers, failover tests) on a deadline-bound exam project. Mitigation: the wire format is identical (verified), so the second provider is a config entry, not a second client; the chain degrades gracefully — remove an entry from the config array and the app is NVIDIA-only or offline-only without touching code. The tight OpenRouter free caps (50 req/day) would make it a weak *primary* but a sound *fallback*. Counter-argument weighed and rejected: single-provider simplicity. Confidence: **high**. Would flip: evidence that NVIDIA's hosted API ends free access for the owner's key AND OpenRouter `:free` tiers are withdrawn — then the chain collapses to offline-only by config, and the owner re-decides.

### API key handling (the user-facing instruction)

- **Keys live in `config/config.local.json`** (git-ignored from the first commit, `.gitignore:25`); the committed template `config/config.example.json` documents the shape without secrets. Shape (defined by the P2 contract, implemented by P5's `AppConfig`):
  ```json
  {
    "aiProviders": [
      { "name": "nvidia",     "baseUrl": "https://integrate.api.nvidia.com/v1", "apiKey": "", "model": "<set by P2 live check>" },
      { "name": "openrouter", "baseUrl": "https://openrouter.ai/api/v1",        "apiKey": "", "model": "<set by P2 live check>" }
    ]
  }
  ```
  Array order is the failover chain order. Keys are **never hard-coded, never logged, never committed** (re-verified in P15, which now audits *both* keys).
- **When each key is required — relay to the owner:** *P0–P4 need no keys. Between P4 and P5: (1) REQUIRED — paste your NVIDIA key into `config/config.local.json` under the `nvidia` entry (you already hold it in `.opencode/secrets/`; copy it yourself — agents cannot access that folder); (2) RECOMMENDED — sign up free at openrouter.ai, create an API key, paste it under the `openrouter` entry. The app works with either entry alone; the OpenRouter key enables the fallback chain. P5's live smoke test exercises the first configured provider and, if its key is present, the failover to the second.*
- **Model names are config, not code:** P2 sets default model slugs from a live catalog check; the plan deliberately pins none (model churn). P5 updates `config/config.example.json` to this shape.

Superseded: the original Gemini-based Route C record and its Google-key timing instruction (2026-11-01) — retained in git history for audit.

## Findings Backlog (ranked, Level 3)

Gaps, risks, and contradictions found in `docs/oop-final-exam-guidelines.md` and `docs/codequest-proposal.md` (all `file:line` read this session), ranked by **score = impact × confidence ÷ effort** (1–5 each; effort 5 = largest). The phase(s) resolving each finding and the responsible agent are named; all are scheduled in this plan.

| # | Finding (evidence) | Imp | Conf | Eff | Score | Resolved in | Owner |
|---|---|---|---|---|---|---|---|
| 1 | **Answer-key review vs auto-scoring contradiction**: proposal line 23 requires AI answer keys be checked *before* a quiz is available to students, but line 51 advertises "Automatic scoring" with no review step or status field anywhere | 5 | 5 | 3 | 8.3 | P2 (status workflow in contract), P13 (review/approve screen) | contractor / frontend |
| 2 | **.NET 8 and .NET 9 end support Nov 10, 2026** (Microsoft support policy, updated 2026-10-25); a default ".NET 8 LTS" scaffold choice would target an EOL runtime in 9 days. .NET 10 LTS runs to Nov 14, 2028 | 5 | 5 | 5 | 5.0 | P0 | backend |
| 3 | **Demo internet dependency**: live AI calls at the exam demo can fail (quota, wifi, provider caps). Proposal line 23 says offline works for saved quizzes — make offline the rehearsed demo path and AI generation the bonus | 5 | 4 | 2 | 10.0 | P5 (provider chain + offline provider), P18 (offline demo script) | middle-end / e2e-tester |
| 4 | **AI provider facts to verify at contract time**: NVIDIA NIM auth header (not shown on the fetched LLM-APIs reference page — `unverified`; expected Bearer-style, must be confirmed), current suitable model slugs on both platforms, OpenRouter free-tier caps already verified (20 req/min; 50/day <10 credits). P2 must re-verify against live docs before any consumer code | 4 | 4 | 2 | 8.0 | P2 | contractor |
| 5 | **User accounts in schema but no login feature listed**: proposal line 19 stores "user accounts" yet the features table (lines 50-54) has no login; a dead table weakens 3NF scrutiny (rubric 20 pts) and instructor Q&A | 3 | 5 | 2 | 7.5 | P1 (schema), P7 (minimal login) | backend / frontend |
| 6 | **WinForms vs browser e2e tooling**: `e2e-testing`/`playwright-cli` are web-oriented; e2e discipline must adapt to scripted desktop checklists + unit tests (see Verification Adaptation) | 3 | 5 | 2 | 7.5 | P7 onward | e2e-tester |
| 7 | **Explainability constraint**: guidelines lines 170-176 — instructor code review; students must explain their own code. Over-engineering (DI containers, EF Core, clever patterns) is a graded liability. Decision: plain ADO.NET over Microsoft.Data.Sqlite (proposal line 39), small repository layer, no ORM | 4 | 4 | 1 | 16.0 | All phases | all implementers |
| 8 | **CRUD/search/filter is 10 of 25 functionality pts** (guidelines lines 88-91) — the DataGridView management module deserves first-class phase treatment, not an afterthought | 4 | 5 | 2 | 10.0 | P8 | frontend |
| 9 | **Sample records are a named deliverable** (guidelines line 147) — seed data must ship as a committed SQL script, not ad-hoc DB state | 3 | 5 | 2 | 7.5 | P1 | backend |
| 10 | **iOS-style UI in WinForms is non-trivial** (manual custom colors/panels/buttons; default-styled forms read as template work — rubric UI 10 pts). Design-of-record phase must precede all UI phases; polish/unslop pass before demo | 4 | 4 | 3 | 5.3 | P6, P16 | designer / frontend |
| 11 | **Proposal feature table is a `[TABLE]` placeholder** (line 26): exact feature list must be confirmed from prose before design | 2 | 5 | 1 | 10.0 | P6 | designer |
| 12 | **Testing is only 5 rubric pts** (guidelines line 77): scope testing to xUnit unit tests for scoring/validation + scripted checklists; no test pyramid | 2 | 4 | 1 | 8.0 | P3, P14, P18 | middle-end |
| 13 | **SQLite FK enforcement is off by default** — `PRAGMA foreign_keys = ON` must run on every connection or referential integrity (rubric 3 pts) is not actually enforced | 4 | 4 | 1 | 16.0 | P1 (doc), P4 (connection factory) | middle-end |
| 14 | **CI actions run on Node.js 24 via deprecation path** — `actions/checkout@v4` and `actions/setup-dotnet@v4` emit a Node 20 deprecation notice (agent-push report, CI run 41300422777, 2026-11-01). Informational, non-blocking; do not bump reactively without a search. Revisit when a runner update forces it or at P18 CI verification | 1 | 3 | 1 | 3.0 | P18 (verification pass) | agent-push |

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
| P2 | `contracts/ai-quiz-api.md` (NVIDIA + OpenRouter) | contractor | light — single artifact, no behavior | cheap — non-functional artifact | no | none |
| P3 | Domain layer (OOP core) | middle-end | heavy — data model + many files | expensive — data model | deferred (no user path) | none |
| P4 | Data access layer | middle-end | heavy — data access, FK/PRAGMA, parameterization | expensive — data | deferred (no user path) | none |
| P5 | AI provider chain + offline provider services | middle-end | heavy — new external API + error handling | expensive — external API behavior | deferred (service-level unit tests; user path arrives P13) | none |
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

## Phase P2 — AI Provider Integration Contract (contract-first)

- **Agent:** contractor. **Depends on:** P0 (repo exists); **must complete before** P5 and P13 (consumers).
- **Files (creates):** `contracts/ai-quiz-api.md` (supersedes the never-written `contracts/gemini-quiz-api.md`; the provider change is recorded in the API Decision Record). **Verified inputs (fetched 2026-11-02, vendor-primary):** NVIDIA NIM — `POST https://integrate.api.nvidia.com/v1/chat/completions`, instruct-model catalog published at docs.api.nvidia.com (updated 2026-10-23); OpenRouter — OpenAI-SDK drop-in at base `https://openrouter.ai/api/v1` with `Authorization: Bearer`, `:free` model variants at 20 req/min and 50 req/day (<10 credits purchased).
- **Contract contents (ONE wire contract, both providers):** the OpenAI chat-completions request/response shape both platforms accept (verified) — endpoint base URLs, per-provider auth headers, model-slug-as-config policy (plan pins no slug; defaults come from P2's live check); request JSON (prompt structure demanding strict-JSON quiz output: question text, 4 choices, correct index, explanation, difficulty, topic; no reliance on provider-specific JSON modes); response→DTO mapping (`choices[].message.content` → strict-JSON parse); **error classes:** HTTP 4xx/5xx, auth failure (401/403), quota/rate (429 — NVIDIA quota, OpenRouter free caps, with `Retry-After`/`X-RateLimit-*` where documented), malformed/non-JSON completion, timeout, empty provider list; **provider-chain semantics:** iterate configured providers in config order, first success wins, empty-key providers are skipped, all-fail → typed failure with per-provider reasons; **quiz status workflow `Draft → Approved` unchanged** (backlog #1: AI quizzes are born Draft, only Approved quizzes are takeable — proposal line 23); config file shape for `config/config.local.json` (see API Decision Record).
- **Size: light** — single artifact, no behavior; routed to `contractor` (sole contract author per roster). **Check: cheap** — non-functional artifact; Reviewer verifies completeness and fidelity to the two vendors' docs. **e2e:** no. **UX review:** none.
- **Reviewer web searches (mandatory):** (1) confirm NVIDIA NIM auth header + endpoint against live docs (the fetched reference page omits the auth scheme — `unverified`, must be closed here); (2) confirm OpenRouter chat-completions compatibility + auth headers via live docs; (3) fetch both live model catalogs (`https://openrouter.ai/api/v1/models` and NVIDIA's catalog) and fix default model slugs suitable for strict-JSON quiz generation; (4) re-confirm OpenRouter free-tier limits. Update the contract with dated sources before P5 starts.
- **Acceptance criteria:** contract file exists under `contracts/` and states both endpoints, auth, the shared request/response JSON, DTO mapping, the six error classes, provider-chain semantics, the Draft→Approved workflow, and the config shape; the NVIDIA auth `unverified` item is confirmed with a dated source or recorded as unresolved with the chain's skip-on-empty-key behavior as the fallback; P5's delegation may not be issued until this file is complete — no consumer phase may begin against a nonexistent contract.

## Phase P3 — Core OOP Domain Layer

- **Agent:** middle-end. **Depends on:** P1 (schema defines the data model).
- **Files (creates under `src/CodeQuest/`):** `Models/UserAccount.cs`, `Models/Topic.cs`, `Models/Quiz.cs`, `Models/Question.cs` (abstract base), `Models/MultipleChoiceQuestion.cs`, `Models/TrueFalseQuestion.cs` (derived), `Models/Choice.cs`, `Models/QuizAttempt.cs`, `Models/SubmittedAnswer.cs`; `Models/Difficulty.cs`, `Models/QuizStatus.cs` (enums matching P1 CHECK constraints); `Services/IQuizProvider.cs` (abstraction — P5 adds two implementations), `Services/IScoringService.cs`, `Services/ScoringService.cs`, `Data/IQuizRepository.cs` (+ repository interfaces per entity, consumed by P4); **tests:** `tests/CodeQuest.Tests/ScoringServiceTests.cs`.
- **Rubric mapping (guidelines lines 36-41, 104-108):** encapsulation = private fields + public properties on every model; inheritance = `Question` base → two derived classes; polymorphism = overridden behavior (e.g., `ValidateAnswer()` / display formatting) across derived classes; abstraction = `IQuizProvider`, `IScoringService`, repository interfaces. These four must be pointable in code review — implementers add a short comment header per concept so the instructor walkthrough is easy (guidelines line 170: students must explain their own code).
- **Size: heavy** — data model + many files. **Check: expensive** — data model. **e2e:** deferred (no user path). **UX review:** none. **Reviewer web searches:** none — internal facts.
- **Acceptance criteria:** project builds; `ScoringServiceTests` covers: all-correct, mixed, none-correct, empty-attempt (≥4 cases, each independently checkable); `MultipleChoiceQuestion` and `TrueFalseQuestion` both compile as `Question` subclasses and override at least one member; every model has private fields with public properties (grep-able); no model references SQLite or HttpClient (separation of concerns — rubric line 119).

## Phase P4 — Data Access Layer

- **Agent:** middle-end. **Depends on:** P1 (schema), P3 (interfaces).
- **Files (creates under `src/CodeQuest/`):** `Data/Db.cs` (connection factory: opens `SqliteConnection`, runs `PRAGMA foreign_keys = ON` on **every** open — verified requirement, sqlite.org/foreignkeys.html §2; loads DB path from config), `Data/Repositories/TopicRepository.cs`, `QuizRepository.cs`, `QuestionRepository.cs`, `AttemptRepository.cs`, `UserRepository.cs` — each implementing its P3 interface; **tests:** `tests/CodeQuest.Tests/RepositoryTests.cs` (against a temp DB built from `database/schema.sql` + `database/seed.sql`).
- **Constraints honored:** every query parameterized (`command.Parameters.AddWithValue("$name", …)` per Microsoft Learn overview, read 2026-11-01) — zero string interpolation into SQL; transactions for multi-row writes (quiz + questions + choices; attempt + answers); **CLR type mappings as documented in P3's model headers** (DateTimeOffset as ISO-8601 UTC text, `bool` for `is_correct`, enums for difficulty/status — P4 binds accordingly, it does not re-invent mappings); **R-2 #3 write-time validation** (an answered question must belong to the attempt's quiz); SQLite constraint violations (UNIQUE per R-2 #5/#6, FK per R-2 #1) surface as distinct, catchable exceptions — repositories never swallow them (P8 maps them to friendly messages).
- **Size: heavy** — data access + FK/PRAGMA + parameterization across repositories. **Check: expensive** — data behavior. **e2e:** deferred (no user path). **UX review:** none. **Reviewer web searches:** none — external facts already verified this session (Microsoft Learn, sqlite.org).
- **Acceptance criteria:** repository tests pass against a seeded temp DB; a grep for string-concatenated SQL in `Data/` returns nothing (all SQL is constant text + parameters); deleting a topic that has quizzes **throws** (FK enforced, proving PRAGMA is on); a quiz+questions+choices write is atomic (transaction test: forced failure leaves no partial rows); CRUD exists for topics, quizzes, questions/choices, attempts.

## Phase P5 — AI Provider Chain & Offline Quiz Services

- **Agent:** middle-end. **Depends on:** P2 (contract — hard gate), P4 (repositories). **⚠ OWNER KEYS REQUIRED HERE** (see API Decision Record — between P4 and P5: NVIDIA key required; OpenRouter key recommended for the fallback).
- **Files (creates under `src/CodeQuest/`):** `Api/ChatCompletionsClient.cs` (one HttpClient wrapper, OpenAI chat-completions wire format per contract: per-provider base URL, auth header, model slug, timeouts; maps HTTP status, auth failure, 429/quota, malformed JSON, timeout, and empty-completion to the contract's error classes); `Api/ChatCompletionsDtos.cs` (request/response DTOs per contract); `Services/AiQuizProvider.cs` (implements `IQuizProvider`: builds the strict-JSON quiz prompt from topic/difficulty/count, walks the configured provider chain in order — skipping empty-key entries — parses the first success, maps to `Quiz` with `QuizStatus.Draft`); `Services/OfflineQuizProvider.cs` (implements `IQuizProvider`: returns seeded/approved quizzes from DB — the last-resort fallback); `Services/QuizGenerationService.cs` (orchestrator: AI chain first → typed failure with per-provider reasons if all fail, never crash); `Services/AppConfig.cs` (reads `config/config.local.json` once; keys never logged); **edits** `config/config.example.json` to the new two-provider shape and refreshes the two stale Gemini comments (`.gitignore:22`, `src/CodeQuest/Api/.gitkeep:1` — R-3/B1); **tests:** `tests/CodeQuest.Tests/AiQuizProviderTests.cs` (mocked `HttpMessageHandler`: valid response per provider, NVIDIA 429 → OpenRouter success (chain failover), all providers fail → typed failure, malformed JSON, 401 auth failure, timeout, empty-key provider skipped, no providers configured → typed failure).
- **Size: heavy** — new external API + error-handling design. **Check: expensive** — external API behavior. **e2e:** deferred — user path arrives at P13; verification here is mocked unit tests + one **optional** live smoke call per configured provider (documented output in the phase report). **UX review:** none. **Reviewer web searches:** re-verify both endpoint shapes, auth headers, and model defaults against live docs (same searches as P2) before PASS.
- **Acceptance criteria:** mocked tests cover every contract error class and the chain-failover case, and pass; a valid mocked response produces a `Quiz` object with questions, choices, correct answers, explanations, and `Status == Draft`; with no provider keys configured, `QuizGenerationService` returns a typed failure (not an exception); with only the NVIDIA key set, the chain skips the OpenRouter entry without error; the API key strings appear nowhere in source, logs, or test output (grep); `config/config.example.json` matches the contract's config shape; live smoke call (if keys present) returns a parseable quiz and its raw JSON is saved to the phase report for the contract's mapping section, with each configured provider's model slug validated by that real generation call — not by NVIDIA's public `/v1/models` listing, which omits documented slugs (R-3/S1).

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
- **Size: heavy** — new screens, consumes external API, state change (Draft→Approved). **Check: expensive** — external API + state change. **e2e:** **yes** (scripted, two paths: **online** — generate live quiz → lands as Draft → review → approve → appears takeable; **offline** — kill network → generate shows a clear typed failure, app stays usable → seeded offline quiz still fully takeable). **UX review:** **yes**. **Reviewer web searches:** re-verify both AI provider endpoints/auth/models unchanged (same searches as P2/P5) before PASS.
- **Acceptance criteria:** generation produces a Draft quiz whose questions/choices/explanations render in the review screen; approval flips status and the quiz becomes takeable (verify in P9 flow); a Draft quiz is never startable; offline/quota failure shows the typed failure message and never crashes or leaves a half-saved quiz; e2e checklist + screenshots recorded for both paths.

## Phase P14 — Validation & Error-Handling Sweep

- **Agent:** middle-end. **Depends on:** P7–P13 existing (sweep across them).
- **Files (edits across `src/CodeQuest/UI/` and `Services/`):** consistent validation helpers (one shared validator, applied to every input form), consistent exception handling (no silent catch; user-facing messages per rubric line 56: appropriate messages for successful *and* unsuccessful operations), **read-path operational DB errors surface as friendly messages — missing schema, locked/corrupt DB file, and similar `SqliteException` operational cases never reach the user raw (P4 review advisory, 2026-11-02)**, success feedback (subtle toast/status text per spec) on every create/update/delete, logging hook (file-based, no key material) for unexpected exceptions.
- **Size: heavy** — spans many files. **Check: expensive** — validation/error-handling behavior (2 rubric pts + feeds UX 3 pts). **e2e:** **yes** — invalid-input paths on every form (empty, overflow, duplicate, FK-blocked delete). **UX review:** **yes** — message quality and feedback consistency. **Reviewer web searches:** none mandatory.
- **Acceptance criteria:** every text input has declared validation (required, length, numeric range as applicable) and rejects bad input with a specific message; every CRUD action shows success feedback; no form can be crashed from the keyboard in a 5-minute adversarial pass (recorded); unexpected exceptions surface a friendly dialog + log line, never a raw stack trace to the user; grep finds no empty catch blocks.

## Phase P15 — Security Review

- **Agent:** security. **Depends on:** P5 (key handling exists), P14 (validation in place).
- **Scope (compact threat model for a local desktop app):** secret handling — **both AI provider keys** (NVIDIA, OpenRouter) present only in git-ignored `config/config.local.json`, never in source/logs/exceptions, git history clean (`git log -p` scan for both key patterns since P0); SQL injection — all queries parameterized (audit `Data/` — rubric line 35); FK/referential integrity enforced; input validation coverage; no PII beyond a display name (data-minimization note for documentation); exception messages never leak connection strings.
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
3. **AI provider economics** (owner directive 2026-11-02): NVIDIA key is in hand; OpenRouter `:free` fallback is verified at 20 req/min, 50 req/day without credits — ample for generation demos, never a primary. The plan does not depend on either free tier persisting: P5's chain degrades to offline-only by config, and P18 rehearses that path. If both AI routes die, the owner re-decides; the `IQuizProvider` seam absorbs it without redesign.
4. **Exam date** is unknown to this plan; phase order is dependency-driven, and P8 (CRUD) + P9 (quiz-taking) are deliberately front-loaded after the shell so the highest-rubric-value modules exist early even if time compresses later phases.
5. The proposal's `[TABLE]` placeholders (lines 26, 31, 48) are formatting artifacts; P6 resolves the feature list from prose and the Orchestrator should confirm the summary above matches the user's intent at P6 review time.
