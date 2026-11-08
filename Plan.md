# Plan.md — CodeQuest: AI topic creation, gamification, dark theme

**Status:** active · **Owner:** Lead · **Created:** 2026-11-08
**Requested by owner (this task):** (1) adding a new topic = describe it, AI generates it;
(2) level + scoreboard for fun; (3) achievements section the app records;
(4) modern dark theme — mind shadow/border/typography/spacing, "no AI slop".
**Owner answers to clarifying questions (binding):**
- Scoreboard = **leaderboard of the users' past scores** (accounts on this machine, ranked).
- Level rules = **XP per correct answer, weighted by difficulty** (recommended option).
- AI topic flow = **preview then confirm** before save.
- Dark theme = **dark only**, light theme is removed.

---

## 1. Goal and acceptance criteria

Make CodeQuest feel like a game (progress you can see, a scoreboard, achievements) and
modernize it to a disciplined dark theme, while extending the proven AI chain to author topics.

**Acceptance criteria (observable):**

| # | Criterion |
|---|---|
| A1 | Topics → New topic: user types a short description, "Generate with AI" (off-UI-thread, busy state) produces name/category/description as an editable preview; Save runs the existing duplicate-name validation. AI failure shows a typed failure line; fields stay manually editable. |
| A2 | Completing a quiz awards XP = correct answers × difficulty weight (Easy 10 / Medium 20 / Hard 30); the result screen shows XP earned, level progress, and any newly unlocked achievements. |
| A3 | XP/level are derivable from stored attempts (no redundant XP column); level thresholds: cumulative XP to reach level n = 25(n−1)(n+2) → L2@100, L3@250, L4@450, L5@700, L6@1000. |
| A4 | New "Scoreboard" module ranks **all accounts** by total XP (rank, player, level, XP, quizzes taken), highlights the signed-in row, and shows the signed-in user's level + progress. |
| A5 | New "Achievements" module shows the catalog with locked/unlocked state and unlock dates; unlocks are recorded in the `user_achievements` table (PK (user_id, achievement_key)); re-completing never duplicates. |
| A6 | Whole app renders in the dark theme (re-issued token values); no light-mode remnants (checked by paint audit); cards gain the hairline-border elevation treatment; dialogs get a subtle native drop shadow; design spec §2 re-issued. |
| A7 | `database/schema.sql` gains `user_achievements`; an idempotent startup migration adds it to **existing** databases without touching existing rows (attempts must survive — they feed the scoreboard). |
| A8 | New unit tests pass (XP/level math, achievement evaluation incl. idempotence, topic JSON strict parser incl. category enum, leaderboard aggregation, migration idempotence); full suite green; build clean. |

**Non-goals:** light/dark toggle, account registration UI, streaks/daily bonuses, cloud sync,
XP write-back into `quiz_attempts` (XP stays derived), per-question live XP animation,
changes to the quiz generation contract (`contracts/ai-quiz-api.md` semantics preserved).

## 2. Confirmed findings (evidence)

- **Stack:** WinForms, `net10.0-windows`, SQLite (`Microsoft.Data.Sqlite`), xUnit tests,
  solution `CodeQuest.sln`; version 1.0.4 in `src/CodeQuest/CodeQuest.csproj`. Working tree clean.
- **Design system:** `UITheme.cs` is the single token source (spec §2 discipline: no hard-coded
  colors in forms). Re-issuing token values re-skins the app; painters must be audited for
  light assumptions (`ControlPaint.Dark`, white literals, `SystemColors`).
- **`CardPanel`** (recipe 4.2) has **no border/shadow** — light theme separated white cards from
  `#F5F5F7` by contrast alone. Dark needs the §3.6 "borders" treatment: 1px hairline on cards.
- **DB:** `Db` never creates tables; `Program.cs` blocks when the DB file is missing. Existing
  `codequest.db` holds attempts → new table must come from an idempotent `CREATE TABLE IF NOT
  EXISTS` migration at startup, plus `schema.sql` for fresh installs.
- **XP derivation:** `quiz_attempts.score` = correct count; difficulty on `quizzes`; users on
  `users.display_name`. Leaderboard = JOIN + `SUM(score × multiplier)`; nothing stored (consistent
  with the plan's normalization rulings, e.g. R-2 #1).
- **AI pattern to reuse:** `ChatCompletionsClient` (one wire client, per-provider parameters),
  `QuizGenerationService` (chain: config order, skip rules, first success wins, typed failures,
  never throws to UI), `StrictQuizJsonParser` (fence-stripping, whole-object JSON, field rules),
  `GenerateQuizForm` (off-UI-thread `Task.Run` + `ContinueWith` + busy state + typed banner).
- **Topic flow today:** `TopicManageForm` dialog = Name + 2-value Category combo
  (`Programming`/`Cybersecurity`, schema CHECK); description column exists but the form saves
  `string.Empty`; duplicate name is validated inline (R-2 #5) and caught as
  `DuplicateEntityException`.
- **Attempt write point:** `QuizTakeForm.Submit()` — one `AddWithAnswers` write, then result view.
  This is where XP/achievement evaluation hooks.
- **Nav:** `MainForm.Modules` currently has 5 entries (TakeQuiz, History, Reports, GenerateQuiz,
  Topics); module panels persist, reload data on show (`IModulePanel.RefreshData`).
- **Seed data:** 1 user, 5 topics, 5 quizzes, 4 attempts — enough to demo the scoreboard.

## 3. Requirements (functional)

- **FR1 — AI topic creation (preview then confirm):** In the New-topic dialog: description field
  (required, 5–300 chars) + "Generate with AI" primary button. Generation walks the same provider
  chain; strict JSON response `{name (1–60 chars), category ("Programming"|"Cybersecurity"),
  description (10–200 chars)}`. Result fills Name/Category/Description fields, all editable
  (manual fallback when AI is down). Save keeps the existing duplicate/empty validation.
  Edit-topic flow unchanged (manual).
- **FR2 — XP and levels:** XP awarded on attempt submit = correct × weight(Easy 10/Medium 20/
  Hard 30). Levels from cumulative thresholds 25(n−1)(n+2). Level visible in the sidebar account
  block (chip + progress), on the Scoreboard, and on the result screen.
- **FR3 — Scoreboard module:** leaderboard over **all users**: rank, display name, level, total
  XP, quizzes taken — ordered by XP desc (tie: more quizzes, then name). Signed-in row visually
  highlighted. A personal summary card (level, XP, progress to next level) sits above the table.
- **FR4 — Achievements module:** code-defined catalog (key, title, description, glyph, rule):
  first-quiz, perfect-run (100% any), perfect-hard (100% Hard), 10-quizzes, 30-quizzes,
  50-correct, level-5, level-10, 3-distinct-topics, both-categories. Evaluated after each attempt
  submit; unlocked rows recorded in `user_achievements` with timestamp. UI: card grid, locked
  (muted) vs unlocked (accent/semantic) with unlock date.
- **FR5 — Dark theme (dark only):** re-issue every token (tables below); keep the 8px grid,
  Segoe UI scale, 12/8/4 radii (typography/spacing discipline is *not* changed); cards gain 1px
  `BorderHairline`; dialogs gain `CS_DROPSHADOW`; all painters audited; `designs/codequest-ui-spec.md`
  §2 re-issued (the "dark mode is out of scope" note is superseded by owner direction).

## 4. Dark token starting point (SUPERSEDED by the validated D4 amendment)

> **Supersession note (2026-11-08, post WP2):** the UX/UI Designer computed real WCAG ratios and
> re-issued several starting values below. The **binding** palette is now
> `designs/d4-dark-theme-amendment.md` §1 (applied to `designs/codequest-ui-spec.md` by WP3).
> Key corrections: `BorderControl #646E82` (planned `#4A5264` failed 3:1), `AccentDisabled
> #262938` (planned value read as enabled), `Danger fill #CF3530` (planned value failed the
> dialog boundary floor), new `DangerHover/Pressed/Disabled`, `SegmentHover #2C3140`,
> `SeparatorHairline #2A2F3A`, tiles re-pinned (`TileAmber #A65F00`, `TileGreen #178146`;
> red/blue kept), `Level*` re-pinned to the text variants, new `AccentText`/`SurfaceRaised`/
> `BorderHairline` tokens. The table below is retained as the original starting point.

Original starting point (designer validates contrast; implementer uses names only):

| Role | Token | Dark value (start) |
|---|---|---|
| Window/sidebar | `Background` | `#101216` |
| Cards/inputs | `Surface` | `#171A20` |
| Dialogs/raised | `SurfaceRaised` (new) | `#1E222B` |
| Hover/selected fill | `Fill` / `FillPressed` | `#232833` / `#2A303D` |
| Input borders (≥3:1 on Surface) | `BorderControl` | `#4A5264` |
| Card hairline (new) / separators | `BorderHairline` / `SeparatorHairline` | `#262B36` |
| Text primary/secondary/tertiary | | `#E8EAF0` / `#9BA3B4` / `#6B7385` |
| Accent fill (white label 4.8:1) | `Accent`/`Hover`/`Pressed`/`Disabled` | `#6C5CE7` / `#5A4BD1` / `#493BB8` / `#3F3B5E` |
| Accent as text on dark (≥4.5:1) | `AccentText` (new) | `#A79BF5` |
| Selected nav bg | `AccentTint` / `NavItemPressed` | `#2A2749` / `#262244` |
| Success fill/text/tint | | `#2FA857` / `#4CC27A` / `#16301F` |
| Danger fill/text/tint | | `#C62B25` / `#F07A72` / `#3A1A18` |
| Warning fill/text/tint | | `#B07C10` / `#E5A83B` / `#38290F` |
| Tiles / difficulty colors | re-tuned for dark, white large text ≥3:1 | Red `#E14B5A` · Blue `#3D7BF0` · Amber `#E09413` · Green `#27A45C` |

Rules: AA for all text (4.5:1; large/bold 3:1), non-text borders ≥3:1; state never color alone;
accent only on primary action/current selection; tints become dark washes; **no gradients, no
glow, no decorative color** — restraint is the anti-slop bar.

## 5. Architecture decisions

- **New tables:** only `user_achievements (user_id, achievement_key, unlocked_at)`,
  PK (user_id, achievement_key), FK → users ON DELETE CASCADE. XP/levels/leaderboard derived.
- **Migration:** `Data/DatabaseMigrator.EnsureSchema(Db)` — idempotent DDL, called in `Program.cs`
  right after the DB-file check; never touches data; safe to run every launch.
- **Services (all behind interfaces, matching the codebase pattern):**
  - `ILevelService` + `LevelService` (pure math: XP→level, progress; difficulty weights).
  - `IGamificationRepository` + `GamificationRepository` (leaderboard rows, per-user XP/quizzes
    aggregate, achievement get/add-if-new). `AttemptRepository` untouched.
  - `IAchievementEvaluator` + `AchievementEvaluator` (catalog in code; input = user aggregates +
    level; output = newly unlocked keys).
  - `ITopicGenerationService` + `TopicGenerationService` (chain semantics copied from
    `QuizGenerationService`) + strict `TopicDraftJsonParser`.
- **UI:** new `ScoreboardForm`, `AchievementsForm` (`ModulePanel` subclasses, persistent panels,
  reload on show); `MainForm` gains two nav entries + sidebar level chip; `TopicFormDialog`
  reworked for describe→generate→preview; `QuizTakeForm` result view gains XP/level/achievement
  reveal. All colors/fonts/spacing via `UITheme` only.
- **Why not store XP:** attempts are the source of truth; deriving avoids drift and keeps the
  3NF story intact (normalization docs).

## 6. Work packages (order, owners, dependencies)

| WP | Owner | Scope (files) | Depends on |
|---|---|---|---|
| WP1 | Implementer | Gamification foundation: `Models/Gamification.cs` (records), `Data/IGamificationRepository.cs` + `Data/Repositories/GamificationRepository.cs`, `Data/DatabaseMigrator.cs`, `Services/LevelService.cs`, `Services/AchievementEvaluator.cs` (+ `IAchievementEvaluator`), `Program.cs` hook, `database/schema.sql`, tests (`LevelServiceTests`, `AchievementEvaluatorTests`, `GamificationRepositoryTests`, `DatabaseMigratorTests`) | — |
| WP2 | UX/UI Designer | Read-only spec: dark token re-issue (§2 amendment with contrast math), Scoreboard/Achievements/New-topic-dialog/level-chip/result-reveal screen specs as recipes in the existing spec format | — |
| WP3 | Implementer | Dark theme application: `UITheme.cs` re-issue + `CardPanel` hairline border + dialog `CS_DROPSHADOW` + painter audit across **existing** UI files + `designs/codequest-ui-spec.md` §2 update | WP2 |
| WP4 | Implementer | AI topic generation: `Services/TopicGenerationService.cs` (+ interface, parser, outcome types), `UI/TopicManageForm.cs` dialog rework, `MainForm` wiring of the service, tests (`TopicGenerationTests`) | WP2 (dialog recipe), WP3 (dark tokens in place) |
| WP5 | Implementer | Gamification UI: `UI/ScoreboardForm.cs`, `UI/AchievementsForm.cs`, `MainForm` nav + sidebar level chip, `QuizTakeForm` result XP/level/achievement reveal, tests where feasible | WP1, WP2, WP3 |
| WP6 | Lead | Integration: build, full test run, polish pass | WP1–WP5 |
| WP7 | QA | Functional verification against A1–A8 | WP6 |
| WP8 | Reviewer | Independent diff review (before any version bump/release; owner directive) | WP7 |

File ownership: WP4 owns `TopicManageForm.cs`; WP5 owns `MainForm.cs` + `QuizTakeForm.cs` + the
two new screens; WP3 touches existing painters but completes **before** WP4/WP5 start.
**Scheduling correction (2026-11-08):** WP4 and WP5 run **sequentially** (WP4 first), not in
parallel: both would need solution builds/tests, and two concurrent `dotnet build` runs share
the same `obj/`/`bin/` outputs — the source write scopes are disjoint, the build artifacts are
not. Any new control painters must use tokens only (a short audit of the new files is folded
into WP6).

## 7. Risks

- **Dark paint audit completeness** — resolved by WP3's per-file audit table (28 files, 0 errors,
  135/135 tests); QA (WP7) still does the visual A6 walk-through — no screenshot verification was
  possible during WP3.
- **Contrast regressions** — the D4 amendment's computed ratios are the reference; QA spot-checks.
- **Migration on existing DB** — idempotent `CREATE TABLE IF NOT EXISTS`; migrator tests cover
  re-run and data preservation; verified by WP1 tests.
- **AI output quality for topics** — strict parser + preview-then-confirm (owner's choice) guards it.
- **WinForms thread rules** — generation follows the proven `GenerateQuizForm` pattern.

### Found defects and rulings (log)

- **Review WP8 (independent, 2026-11-08): verdict DO-NOT-SHIP → both findings fixed same day.**
  - **MAJOR-1 (fixed):** `GamificationRepository` compared the perfect-run rule against the
    *answered* count — a partial attempt (1 correct of 5, questions skipped — the player allows
    it) permanently unlocked "Flawless"/"Nightmare Slayer" (the ledger never revises a first
    unlock). The rule now requires `answered == quiz question count && score == question count`
    (the query joins the quiz's question total); regression test
    `GetUserStats_PartialAttemptWithSkippedQuestions_IsNeverPerfect` added.
  - **MINOR-1 (fixed):** the achievement catalog carried emoji glyphs, painted with Segoe MDL2
    Assets (no emoji → tofu/colour fallback, breaking the no-decorative-colour rule). All ten
    replaced with the §5.14 verified codepoints (E768, E73E, E930, E7BC, E8F1, E8FB, E870,
    E945, E81E, EA86).
  - Areas reviewed clean: migration idempotence/FK, leaderboard aggregation, add-if-new
    semantics, threading (topic dialog has stronger guards than its quiz twin), secrets, UI
    wiring, dark re-issue regressions, test quality (one gap — the skipped-questions case —
    now closed).
  - **Delta review verdict: SHIP** for 1.0.4 → 1.1.0 (both fixes confirmed complete and
    correct, incl. an independent re-check of the rule against the seeded data). Remaining:
    the owner's visual spot-check of the MDL2 achievement icons (the only item a human eye
    must confirm) and the release authorization.
- **Pre-existing defect (found in WP3 audit, confirmed against HEAD): FIXED 2026-11-08 (Lead).**
  `QuizTakeForm.HandleTileClick` guarded on `tile.Tag`, which `RenderQuestion` never assigned —
  every mouse tile click no-op'd. The guard now reads `AnswerTile.ChoiceId` (the tile's own
  property, set at render). QA must verify click + keyboard selection end-to-end on the run.
- **Lead ruling (WP3 deviation #6):** `UITheme.Display` (28 pt bold) stays as the result-score
  font (owner fun-UI directive supersedes the amendment's `Title` restatement for that one
  element; recorded in the D4 amendment §2).
- **WP6 polish candidates (accepted, non-blocking):** History/Reports/GenerateQuiz use plain
  square `Surface` panels without the D4 card hairline — unify on `CardPanel` during WP6 if
  cheap; ThemedComboBox native dropdown chrome needs a QA visual check on dark.

### Progress log

- WP1 ✅ (Implementer, Lead-verified: code read, tests re-run).
- WP2 ✅ (Designer; amendment saved to designs/d4-dark-theme-amendment.md; Lead decisions bound).
- WP3 ✅ (Implementer; 0 warnings, 28-file audit table; Lead accepted deviations).
- WP4 ✅ (Implementer phases 1/3/4; Lead completed phase 2 — deleted the leftover
  TempProbeTests.cs, wrote TopicGenerationTests.cs (20 cases: valid/fenced output, every
  strict-parser violation, wire-class passthrough, chain skip/order/terminal rules, request
  guard) — one test expectation corrected against the contract grammar ("nvidia:quota:429").
  Build 0/0; 155/155 tests at that point).
- WP5 ✅ (Lead, parallel where independent): LevelChip.cs, ScoreboardForm.cs,
  AchievementsForm.cs, MainForm integration (§5.2a nav, §4.14 sidebar level block,
  UpdateSidebarLevel), tile-click defect fix, and the §5.7a result reveal (XP line + level row
  + conditional achievement chips, hero card grows to fit, post-submit sidebar refresh,
  graceful degradation when progress recording fails). Build 0 warnings / 0 errors; final
  full-suite run in flight at the time of writing.
- WP6 (Lead integration): pending the final suite result; then WP6 polish candidates if cheap.
- Next: WP7 QA (functional walk-through vs A1–A8) and WP8 Reviewer (independent diff review —
  the owner directive: reviewer BEFORE any version bump/release).

## 8. Verification plan (A# → check)

- A1/A2/A4/A5: QA manual scenarios + unit tests below.
- A3: `LevelServiceTests` (threshold boundaries 99/100/249/250…, level 1 at 0 XP).
- A5: `AchievementEvaluatorTests` (each rule fires / doesn't; idempotent re-run),
  `GamificationRepositoryTests` (add-if-new, dates).
- A6: WP3 audit checklist + designer review of implemented screens.
- A7: `DatabaseMigratorTests` (fresh DB, existing DB re-run, rows preserved); manual: launch on a
  copy of the real `codequest.db`.
- A8: `dotnet build CodeQuest.sln` + `dotnet test CodeQuest.sln` — both green before completion.
