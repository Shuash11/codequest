# CodeQuest — Implementation Plan (Revised 2026-11-07)

## Status

`active`. This revision **replaces the 19-phase process-heavy plan** at the owner's direction ("over-engineered — simplify"). Remaining work is now **6 increments** with proportionate verification. The prior plan's full history lives in git (through commit `a9af1c8` + superseded working-tree notes).

**Owner confirmations (2026-11-07):**
1. "Public APIs for questions" = the **free AI model endpoints** (NVIDIA / OpenRouter) — already built in the provider chain. No new scope.
2. **Manual quiz management stays** (curation/correction of AI output + 14 rubric points for CRUD/search/filter). **AI generation is the headline feature**; users are never *required* to create quizzes by hand.

## Goal

A fun desktop quiz app for programming & cybersecurity (final exam project, C#/.NET WinForms + SQLite, 100 pts). Students pick topic, difficulty, and question count → the system generates the quiz via AI (NVIDIA → OpenRouter → offline seeded bank) → it is reviewed and approved → taken → auto-scored → history and performance reports, fully offline-capable for saved content. Everything delivered must map to the exam rubric and be explainable in an instructor code review.

## Current state (verified 2026-11-07)

- **Built & committed locally** (HEAD beyond `a9af1c8`, ahead of `origin/main` — push gated on the I1 review, not defects): repo+CI (P0), schema+seed+3NF (P1), AI contract (P2/P2.1), OOP domain layer (P3), data access layer (P4), AI provider chain + offline provider + generation service (P5), design spec (P6) + accessibility amendments (P6.1), login+shell (P7), **topic/quiz/question CRUD with search/filter (P8)**, **P8.1 accessibility fixes (commit `33fab04`)**.
- **Tests:** 7 xUnit files (scoring, repositories, AI provider, form validation, list filter, grid row actions, scaffold). Last recorded full run: 88/88 pre-P8.1 (2026-11-06); the P8.1 round replaced the 23 hasher tests with grid-action tests — CI run is the authority.
- **CI:** `.github/workflows/ci.yml` — Windows runner, .NET 10 SDK, build + test. Green through the P7 era on `origin/main` (`ae7dd74`).
- **Local build/test currently impossible:** this machine has .NET SDK **9.0.306**; the project targets `net10.0-windows` (error NETSDK1045). Until the .NET 10 SDK is installed here, **CI is the only verification path**.
- **`config/config.local.json` is still the pre-P2 shape** (no NVIDIA key, no model set) → no live AI generation possible yet.

## Open blockers (owner actions)

1. **Paste AI keys** into `config/config.local.json` (P2 shape): NVIDIA key (required), OpenRouter key (recommended). Needed before I4; the P5 live smoke test also depends on it. Owner pastes personally — agents never touch this file.
2. Approve this revised plan — **superseded 2026-11-07: owner approved execution, I1 started.**

Owner decision (2026-11-07): **verification runs on CI only** — the local .NET SDK stays at 9, so every check lands via GitHub Actions (build+test on .NET 10). Consequence: the red→fix→green loop runs through pushes, so each increment commits in small coherent slices and watches CI per push.

## Binding constraints (ratified decisions that stay)

1. **Provider chain (Route C):** NVIDIA primary → OpenRouter `:free` fallback → offline seeded bank; one OpenAI-compatible client; array order in config = failover order; an entry with an empty key is skipped. Keys live only in git-ignored `config/config.local.json`; never hard-coded, logged, or committed.
2. **Schema (R-2, frozen):** no stored derived columns (score computed via joins); UNIQUE `(topic_id,title)`, `(quiz_id,position)`, `(question_id,choice_text)`; exactly-one-correct-choice enforced at persist time; `explanation NOT NULL` (empty string when none); `topics.category` ∈ {Programming, Cybersecurity}; `position` unique per parent, reorder inside a transaction; `PRAGMA foreign_keys = ON` on every connection.
3. **Draft → Approved:** AI-generated quizzes land as Draft and become takeable only after review/approval (proposal's review-before-availability note). Approval is a status flip only — no audit columns.
4. **Explainability:** plain ADO.NET over `Microsoft.Data.Sqlite`, small repository layer, no ORM, no DI container, no clever patterns — the owner must be able to explain every line.
5. **Tokens-only UI:** no literal color values in form code (grep-verified); all colors via `UITheme.cs` tokens so the grey-theme re-skin (I5) is a one-point change.
6. **Target:** `net10.0-windows`; CI pins `10.0.x`. Version 0.1.0 until I6 passes → **1.0.0**.
7. **Safety:** never delete/overwrite anything you did not create (`docs/`, `plans/`, `designs/`, `contracts/`, `.opencode/`, user configs). Stop and report instead of improvising. Secrets never appear in messages, logs, or commits.

## Requirements → rubric traceability

| Rubric criterion | Pts | Delivered by |
|---|---|---|
| CRUD operations | 10 | P8 (done) + I1 accessibility fixes |
| Search, filtering, report | 4 | P8 search/filter (done) + I3 reports |
| Major modules function | 8 | I2 (quiz taking/scoring/history), I3 (reports), I4 (AI generation) |
| Navigation & integration | 3 | shell (done) + new module wiring I2–I4 |
| 3NF database | 20 | P1 (done); docs finalized I6 |
| OOP concepts | 20 | P3 (done — Question hierarchy, repository/service interfaces); explained I6 |
| UI & usability | 10 | P6/P7 (done) + I1 a11y + I5 grey theme |
| Code quality | 10 | existing conventions + Reviewer gates I1/I2/I4 |
| Testing & error handling | 5 | existing suite + I5 validation sweep |
| Presentation | 10 | I6 docs + demo script + rehearsal |

## Increments

Each increment: implement → focused checks → independent review where named → push → CI green. Flake rule: a step that cannot be reproduced twice is a defect.

### I1 — Land the in-flight work + quick wins
**Depends on:** nothing (verification via CI — owner decision 2026-11-07). **Status: implemented, in independent review** (commits `b90c0eb` plan revision, `33fab04` P8.1 fixes, `1021f44` D1 name-only login, `9db6ecc` D2 CI exe). Push gates on the Reviewer verdict; CI green + exe artifact closes the increment.
- **Scope:** (a) verify/finish the uncommitted P8.1 fixes — keyboard-operable grid row actions with visible focus, accessible custom controls (name = visible label, perceivable checked state), strip-feedback token, editor surface per amended spec; (b) **name-only sign-in (owner directive D1):** `LoginForm` takes a name only; remove `PasswordHasher` + its 23 tests; dispose the seeded hash column usage (schema drop is a separate decision — default: leave column, mark unused); (c) **CI publishes a downloadable .exe (D2):** add `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` + artifact upload to the existing workflow; (d) push the accumulated local work.
- **Accept:** full suite green locally and in CI; keyboard-only user reaches every grid action (×2); UIA names = visible labels; login works name-only end-to-end; CI green **and** produces the exe artifact; demo DB untouched.
- **Review:** Reviewer gate (large accumulated diff: P8 + P6.1 + P8.1 + D1 + D2, never independently reviewed as a whole).

### I2 — Quiz taking, scoring, history & answer review
**Depends on:** I1 pushed (shell + management screens stable).
- **Scope:** `QuizTakeForm` (Approved quizzes only — Draft listed disabled "pending review"; question-by-question navigation with progress; submit → `ScoringService` → persist attempt + submitted answers); `HistoryForm` (past attempts: date, quiz, topic, score; search + topic/score filters); `AttemptReviewForm` (per question: chosen vs correct answer + explanation); duplicate-submit guard; clean mid-quiz exit.
- **Accept:** score = correct/total matches `ScoringService` unit math; attempt rows persist correctly; Draft never startable; history + review work offline; double-submit cannot duplicate; e2e checklist ×2 with screenshots; light ux check against the existing design spec.
- **Review:** Reviewer gate (scoring + new write paths are the highest-risk logic since P5).

### I3 — Reports
**Depends on:** I2 (attempts exist to report on).
- **Scope:** `IReportService`/`ReportService` (attempts per topic: avg/best/latest; per-difficulty; weak-topic list; overall summary) + `ReportsForm` (summary panel, per-topic grid, topic/difficulty filter).
- **Accept:** aggregates match hand-computed seed expectations in unit tests; zero-attempt topic returns a defined empty result; UI numbers equal service numbers; weak-topic list sorted ascending by average; e2e checklist ×2 + screenshots.

### I4 — AI quiz generation & review/approve
**Depends on:** **owner keys pasted** + P5 live smoke (one real generation call per provider, evidence saved); I1–I2.
- **Scope:** `GenerateQuizForm` (topic, difficulty, 5–20 questions, Generate → `QuizGenerationService`; busy indicator animating; typed-failure banner pointing at the offline path); `QuizReviewForm` (list Drafts → inspect AI questions/choices/claimed answers/explanations → approve or edit/correct or discard); Draft/Approved badges in `QuizManageForm`; generation invoked off the UI thread (client can block ≈2 min).
- **Accept:** online e2e: generate → Draft → review → approve → quiz takeable in I2's flow; offline/quota path: clear typed failure, app stays usable, never a half-saved quiz; Draft never startable; e2e ×2 + screenshots.
- **Review:** Reviewer gate; re-verify both provider endpoints/auth/model slugs against live docs before PASS.

### I5 — Hardening: validation sweep, security notes, grey theme
**Depends on:** I1–I4 screens exist.
- **Scope:** one shared validator applied to every input form; friendly messages for all failures incl. operational DB errors (missing schema, locked DB — never raw SQLite errors); no empty catch blocks; status-strip feedback announced to assistive tech; `docs/security-notes.md` (key handling, SQL-injection audit of `Data/`, compact threat model, data minimization); **grey/modern theme (D3):** designer re-issues tokens (fixing contrast findings F3/F7/F8/F9 to ≥ AA) → `UITheme.cs` is the single code revision point → visual re-render of all screens.
- **Accept:** 5-minute adversarial keyboard pass cannot crash any form (recorded); grep clean: no empty catches, no literal colors in form code; key patterns absent from all git history and logs; contrast fixes verified ≥ AA; before/after screenshots; behavior unchanged by the re-skin (spot-check I2 path).

### I6 — Docs, demo readiness & regression
**Depends on:** I1–I5 PASS.
- **Scope:** `docs/erd.md` (matches deployed schema exactly), `docs/normalization.md` (finalize 1NF→3NF), `docs/oop-explanation.md` (each concept → real `file:line`), `docs/system-description.md`, `docs/user-guide.md` (= demo script); full scripted state matrix on a **fresh clone** (build → seed → login → CRUD → offline quiz-taking → history → reports → online AI generation → review → approve → take → reports reflect it; adversarial input pass); **offline demo rehearsal** with network disconnected; validate the CI exe artifact.
- **Accept:** every doc fact verified against the repo; demo script executable by someone who has never seen the code; offline rehearsal zero failed steps; CI green; tag **1.0.0**.

## Verification strategy

- **Local:** source-level checks only (grep sweeps, reading) — the machine has no .NET 10 SDK by owner decision; correctness gates run on CI.
- **CI:** every push must land a green run; that run is the gate, and it is the *only* build+test verification (fresh-runner lesson).
- **User paths:** scripted e2e checklists executed ×2 with screenshots (WinForms — browser automation does not apply; assert on observable state: DB rows, on-screen values, artifacts).
- **Independent review:** named per increment (I1, I2, I4); lighter increments rely on CI + focused self-checks. No per-phase ux cycles — ux verification folds into each increment's checklist plus I5's visual pass.

## Risks

| Risk | Mitigation |
|---|---|
| No local .NET 10 SDK → slow verify loop | Install (blocker #1); CI as fallback |
| Live AI fails at demo time (quota/network/caps) | Offline path is the rehearsed demo; chain degrades by config; seeded bank always works |
| I1 carries a large never-reviewed diff | Reviewer gate before push |
| Theme re-skin scope creep | Tokens-only discipline; `UITheme.cs` single point |
| Name-only login weakens the security story | Owner's explicit call (flagged once in the old plan); `docs/security-notes.md` records the disposition |

## Assumptions

1. Single local user; name-only sign-in per owner directive D1 (PBKDF2 hasher delivered in P7 is removed).
2. Product scope is frozen to I1–I6 — no new features; scope changes go through the Lead and get an increment or a descoped note.
3. The design of record (`designs/codequest-ui-spec.md` + P6.1 amendments) governs new screens; new-screen recipes reuse existing tokens/controls rather than new designer phases.
