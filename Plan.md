# Plan.md — CodeQuest: AI topic creation, gamification, dark theme

**Status:** task 1 (1.1.0) shipped · **task 2 done (in-app update + remembered name)** · **task 3 done (one-line comments)** · **Owner:** Lead · **Created:** 2026-11-08

---

# Task 3 (2026-11-11, active): one-line comments, simple language

**Owner request:** the heavy technical comment banners are disturbing — remove
them; only ONE short, simple, non-technical comment line per file remains; it
names where the 4 OOP pillars reside; the CRUD methods also carry short
comments.

**Owner answers (binding):**
- Scope: the **codebase only (src/CodeQuest)** — test files keep their
  comments.
- OOP tags: **only where a pillar genuinely lives**, per
  `docs/oop-explanation.md` (plain files get no tag).
- Depth: **all** other comments go — banners, inline notes, `///` summaries.
- Sequencing: the task-2 reviews (2 background agents) read the current
  state; fixes land first, then this sweep — findings stay addressable.

**Comment format:** `// <What this file does, plain words>. (<Pillar> — <why>)`
CRUD methods in `Data/Repositories/*.cs` get one short line each
("// Adds a user.", "// Reads all users.", "// Updates a user.",
"// Deletes a user.").

## File → one-line map (authoritative for the sweep)

- Program.cs — "Starts the app: sign-in, then the main window."
- Api/AiDtos.cs — "The data sent to and received from the AI."
- Api/ChatCompletionsClient.cs — "Sends requests to the AI provider. (Abstraction)"
- Data/Db.cs — "Opens the SQLite database."
- Data/DatabaseMigrator.cs — "Adds missing tables to old databases safely."
- Data/DataExceptions.cs — "Clear errors for database problems. (Inheritance)"
- Data/SqlValueMap.cs — "Converts values between the app and the database."
- Data/AttemptAnswerGuard.cs — "Checks answers before they are saved."
- Data/I*Repository.cs (7) — "What the app needs from <X>. (Abstraction)"
- Data/IGamificationRepository.cs — "What the app needs for XP, levels and achievements. (Abstraction)"
- Data/Repositories/*.cs (8) — "Reads and writes <X>. (Abstraction)" + CRUD one-liners
- Models/Choice.cs — "One answer option. (Encapsulation)"
- Models/Difficulty.cs — "The three difficulty levels."
- Models/Gamification.cs — "Level, XP and achievement records."
- Models/MultipleChoiceQuestion.cs — "A question with four options. (Inheritance, Polymorphism)"
- Models/TrueFalseQuestion.cs — "A true-or-false question. (Inheritance, Polymorphism)"
- Models/Question.cs — "The base of every question. (Inheritance, Abstraction, Encapsulation)"
- Models/Quiz.cs — "A quiz with its questions. (Encapsulation)"
- Models/QuizAttempt.cs — "One finished quiz attempt."
- Models/QuizStatus.cs — "A quiz is a draft or approved."
- Models/SubmittedAnswer.cs — "The answers a player picked."
- Models/Topic.cs — "A subject quizzes belong to."
- Models/UserAccount.cs — "A player's profile. (Encapsulation)"
- Services/AppConfig.cs — "Reads the settings file."
- Services/AchievementEvaluator.cs — "Checks which achievements are unlocked."
- Services/IAchievementEvaluator.cs — "The achievement contract. (Abstraction)"
- Services/AiQuizProvider.cs — "Asks the AI to make a quiz. (Polymorphism)"
- Services/AiTopicProvider.cs — "Asks the AI to make a topic."
- Services/ILevelService.cs — "The level contract. (Abstraction)"
- Services/LevelService.cs — "Turns XP into levels."
- Services/IQuizProvider.cs — "The quiz generator contract. (Abstraction)"
- Services/OfflineQuizProvider.cs — "Makes quizzes from the ones already saved. (Polymorphism)"
- Services/QuizGenerationService.cs — "Tries each quiz source until one works."
- Services/IReportService.cs — "The report contract. (Abstraction)"
- Services/ReportService.cs — "Builds the player's reports. (Polymorphism)"
- Services/IScoringService.cs — "The scoring contract. (Abstraction)"
- Services/ScoringService.cs — "Scores an attempt. (Polymorphism)"
- Services/ITopicGenerationService.cs — "The AI topic contract. (Abstraction)"
- Services/TopicGenerationModels.cs — "Data shapes for AI topics."
- Services/TopicGenerationService.cs — "Asks the AI for a new topic."
- Services/UpdateCheckService.cs — "Checks GitHub for a newer version."
- Services/SelfUpdateService.cs — "Downloads and installs the update."
- Services/SessionStore.cs — "Remembers the player's name between launches."
- UI/AchievementsForm.cs — "The achievements screen."
- UI/AnswerTile.cs — "A clickable answer tile."
- UI/AttemptReviewForm.cs — "Shows the answers of a finished attempt."
- UI/CardPanel.cs — "A rounded card. (Inheritance)"
- UI/DialogForm.cs — "The app's message dialog. (Inheritance)"
- UI/FlatButton.cs — "A styled button. (Inheritance)"
- UI/GenerateQuizForm.cs — "The AI quiz creation screen."
- UI/HistoryForm.cs — "The past attempts screen."
- UI/IModulePanel.cs — "The contract every screen follows. (Abstraction)"
- UI/InputArea.cs — "The question input area."
- UI/InputField.cs — "A text field with an error line."
- UI/IosListGrid.cs — "The card list used by the screens."
- UI/LevelChip.cs — "The level badge in the sidebar."
- UI/LiveStatusLabel.cs — "The status line under the header."
- UI/LoginForm.cs — "The name screen. (Inheritance)"
- UI/MainForm.cs — "The main window with the sidebar. (Inheritance)"
- UI/MarkedCheckBox.cs — "A checkbox with a mark."
- UI/NavItemButton.cs — "A sidebar button."
- UI/ProgressTrack.cs — "The progress bar."
- UI/QuizCard.cs — "One quiz row card."
- UI/QuizListFilter.cs — "The filter row of the quiz list."
- UI/QuizTakeForm.cs — "The quiz-taking screen."
- UI/ReportsForm.cs — "The reports screen."
- UI/ScoreboardForm.cs — "The leaderboard screen."
- UI/SegmentedControl.cs — "The two-tab switch control."
- UI/StatusBadge.cs — "A small status label."
- UI/ThemedComboBox.cs — "A styled dropdown."
- UI/TopicManageForm.cs — "The topics screen."
- UI/UiFormat.cs — "Small text formatting helpers."
- UI/UITheme.cs — "All colors, fonts and sizes. (Encapsulation)"
- UI/UpdateDialog.cs — "The update dialog."
- UI/UpdateProgressDialog.cs — "The updating dialog."

## Verification

- `dotnet build` (Debug + Release) 0 warnings 0 errors; full suite 175/175 —
  comments only, zero code changes beyond deleted comment lines.
- grep: no `///` left in src; every file starts with exactly one `//` line.
- Pillar tags cross-checked against docs/oop-explanation.md (which stays the
  detailed reference and is unchanged).

---

# Task 2 (2026-11-11): in-app update flow + remembered name

**Owner request:** (1) updating must not redirect to GitHub — the app shows an
"Updating…" dialog and performs the update itself; (2) the name screen asks
every launch — the name must be asked only once and remembered after the user
has entered it.

## Goal and acceptance criteria

| # | Criterion |
|---|---|
| B1 | The update dialog's primary button ("Update now") never opens a browser when the release carries an installer asset: clicking it shows an "Updating CodeQuest" dialog (download %, then verifying, then installing); when it finishes the app closes, the new installer runs silently, and CodeQuest relaunches by itself. |
| B2 | Download or integrity failure shows a friendly inline message in the same dialog with "Try again"/"Close"; "Close" (and "Cancel" during download) returns to the running session — the update never blocks studying. "Not now" keeps working. |
| B3 | A release **without** an installer asset falls back to the previous browser behavior (documented). |
| B4 | After one successful name sign-in, later launches go straight into the app (no name screen). "Log out" returns to the name screen (prefilled with the last name) and the next launch asks again. A saved name that no longer resolves in the database falls back to the name screen. |

**Assumptions (stated, low-impact):** the in-app update downloads the
release's installer asset (`CodeQuest-Setup*.exe`) and runs it
`/VERYSILENT /NORESTART` — the Inno script keeps `codequest.db` +
`config.local.json` on upgrade (`onlyifdoesntexist`); logout clears the
remembered name (explicit sign-out asks again next launch).

## Approach

- **Update plumbing:** `UpdateCheckService.ParseReleaseJson` now also selects
  the **installer** asset (first `*.exe`, preferring a name containing
  "setup") and its `digest` (sha256, when GitHub supplies one) into
  `UpdateCheckResult.InstallerUrl`/`InstallerSha256`; `DownloadUrl` keeps its
  browser-fallback semantics.
- **`Services/SelfUpdateService`** (static, like `UpdateCheckService`):
  download to `%TEMP%\CodeQuestUpdate\` with progress + cancellation,
  sha256-verify when the API supplies a digest, then write a tiny
  `apply-update.cmd` (wait 2 s → run installer `/VERYSILENT /NORESTART` →
  relaunch `AppContext.BaseDirectory\CodeQuest.exe`) and start it windowless.
  Every failure returns a typed outcome; nothing throws to the UI.
- **`UI/UpdateProgressDialog`**: "Updating CodeQuest" card (UpdateDialog
  family style) — status line + ProgressTrack + Cancel / (on failure)
  Try again + Close. DialogResult.OK only when the installer was launched.
- **`UI/UpdateDialog`**: button text "Update now"; click no longer opens the
  browser when an installer asset exists (browser fallback only for B3).
- **`UI/MainForm.MaybeShowUpdateDialog`**: offer → progress dialog → close
  shell only when the update is actually applying.
- **Remembered name:** new `Services/SessionStore` (instance, injectable
  directory for tests) persisting the last signed-in username at
  `%LOCALAPPDATA%\CodeQuest\session.json`; never throws. `Program.Main`
  auto-resolves the saved name through `UserRepository` (DB failure keeps the
  existing §4.11 behavior), skips the login on success, saves on sign-in, and
  clears on logout (`ReturnedToLogin`); `LoginForm` gains an optional
  prefilled name.
- **Docs:** `docs/release-process.md` re-issued: a release must attach
  `CodeQuest-Setup.exe` (installer) for in-app updates; describes the new
  update flow.

## Work packages (all Lead-direct: tightly coupled UI+Program changes, single writer)

| WP | Scope | Files | State |
|---|---|---|---|
| T2-WP1 | Installer asset selection + tests | `UpdateCheckService.cs`, `UpdateCheckTests.cs` | ✅ 13/13 update tests |
| T2-WP2 | Download/verify/apply service + tests | `SelfUpdateService.cs` (new), `SelfUpdateServiceTests.cs` (new) | ✅ 5/5 |
| T2-WP3 | Update progress dialog + dialog wiring | `UpdateProgressDialog.cs` (new), `UpdateDialog.cs`, `MainForm.cs` | ✅ |
| T2-WP4 | Remembered name | `SessionStore.cs` (new), `SessionStoreTests.cs` (new), `Program.cs`, `LoginForm.cs` | ✅ 8/8 (camelCase fix applied) |
| T2-WP5 | Docs + integration | `docs/release-process.md`, `docs/security-notes.md`, `docs/user-guide.md` | ✅ |
| T2-WP6 | Independent review (download-and-execute path) | — | ⚠️ Reviewer + Security Reviewer never returned (session ids ses_e35760721ffe02HS5fBIdp7Lfo, ses_e35760720ffeRdCcCsxUn4mPXV); the owner directed to proceed; the Lead self-reviewed instead — **independent review reported incomplete** |

## Integration status (2026-11-11)

- Debug + Release builds: 0 errors; full suite 175/175 (was 156 before task 2).
- Task 2 + task 3 committed; pushed on the owner's explicit instruction.
- Not done (owner-authorized only): version bump, tag, GitHub release.
- Manual (owner): GUI walk-through on an installed build — B1 end-to-end
  silent update + relaunch, B4 remembered name, and the visual state of the
  two dialogs (no rendered previews were available).

## Task 3 status (one-line comments): DONE

- 88 source files swept: one plain-language line per file (OOP pillar tags
  only where a pillar lives), all banners/inline/`///` comments removed,
  short CRUD lines in the repositories; tests untouched.
- Verified: `///` count 0, banner count 0, first-line/blank-line structure
  check clean across all 88 files, URLs in strings intact, Debug + Release
  builds 0 errors, 175/175 tests.
- Tooling: throwaway stripper at
  `%TEMP%\opencode\comment-stripper` (string-aware mini-lexer; interpolated
  raw-string handling added after the first run left two files unstripped —
  caught by review greps, sources restored from git, re-run clean).

---

# Task 1 (2026-11-08, shipped as 1.1.0): AI topics, gamification, dark theme

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
