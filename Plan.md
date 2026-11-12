# Plan.md â€” CodeQuest: AI topic creation, gamification, dark theme

**Status:** task 1 (1.1.0) shipped Â· **task 2 done (in-app update + remembered name)** Â· **task 3 done (one-line comments)** Â· **Owner:** Lead Â· **Created:** 2026-11-08

---

# Task 3 (2026-11-11, active): one-line comments, simple language

**Owner request:** the heavy technical comment banners are disturbing â€” remove
them; only ONE short, simple, non-technical comment line per file remains; it
names where the 4 OOP pillars reside; the CRUD methods also carry short
comments.

**Owner answers (binding):**
- Scope: the **codebase only (src/CodeQuest)** â€” test files keep their
  comments.
- OOP tags: **only where a pillar genuinely lives**, per
  `docs/oop-explanation.md` (plain files get no tag).
- Depth: **all** other comments go â€” banners, inline notes, `///` summaries.
- Sequencing: the task-2 reviews (2 background agents) read the current
  state; fixes land first, then this sweep â€” findings stay addressable.

**Comment format:** `// <What this file does, plain words>. (<Pillar> â€” <why>)`
CRUD methods in `Data/Repositories/*.cs` get one short line each
("// Adds a user.", "// Reads all users.", "// Updates a user.",
"// Deletes a user.").

## File â†’ one-line map (authoritative for the sweep)

- Program.cs â€” "Starts the app: sign-in, then the main window."
- Api/AiDtos.cs â€” "The data sent to and received from the AI."
- Api/ChatCompletionsClient.cs â€” "Sends requests to the AI provider. (Abstraction)"
- Data/Db.cs â€” "Opens the SQLite database."
- Data/DatabaseMigrator.cs â€” "Adds missing tables to old databases safely."
- Data/DataExceptions.cs â€” "Clear errors for database problems. (Inheritance)"
- Data/SqlValueMap.cs â€” "Converts values between the app and the database."
- Data/AttemptAnswerGuard.cs â€” "Checks answers before they are saved."
- Data/I*Repository.cs (7) â€” "What the app needs from <X>. (Abstraction)"
- Data/IGamificationRepository.cs â€” "What the app needs for XP, levels and achievements. (Abstraction)"
- Data/Repositories/*.cs (8) â€” "Reads and writes <X>. (Abstraction)" + CRUD one-liners
- Models/Choice.cs â€” "One answer option. (Encapsulation)"
- Models/Difficulty.cs â€” "The three difficulty levels."
- Models/Gamification.cs â€” "Level, XP and achievement records."
- Models/MultipleChoiceQuestion.cs â€” "A question with four options. (Inheritance, Polymorphism)"
- Models/TrueFalseQuestion.cs â€” "A true-or-false question. (Inheritance, Polymorphism)"
- Models/Question.cs â€” "The base of every question. (Inheritance, Abstraction, Encapsulation)"
- Models/Quiz.cs â€” "A quiz with its questions. (Encapsulation)"
- Models/QuizAttempt.cs â€” "One finished quiz attempt."
- Models/QuizStatus.cs â€” "A quiz is a draft or approved."
- Models/SubmittedAnswer.cs â€” "The answers a player picked."
- Models/Topic.cs â€” "A subject quizzes belong to."
- Models/UserAccount.cs â€” "A player's profile. (Encapsulation)"
- Services/AppConfig.cs â€” "Reads the settings file."
- Services/AchievementEvaluator.cs â€” "Checks which achievements are unlocked."
- Services/IAchievementEvaluator.cs â€” "The achievement contract. (Abstraction)"
- Services/AiQuizProvider.cs â€” "Asks the AI to make a quiz. (Polymorphism)"
- Services/AiTopicProvider.cs â€” "Asks the AI to make a topic."
- Services/ILevelService.cs â€” "The level contract. (Abstraction)"
- Services/LevelService.cs â€” "Turns XP into levels."
- Services/IQuizProvider.cs â€” "The quiz generator contract. (Abstraction)"
- Services/OfflineQuizProvider.cs â€” "Makes quizzes from the ones already saved. (Polymorphism)"
- Services/QuizGenerationService.cs â€” "Tries each quiz source until one works."
- Services/IReportService.cs â€” "The report contract. (Abstraction)"
- Services/ReportService.cs â€” "Builds the player's reports. (Polymorphism)"
- Services/IScoringService.cs â€” "The scoring contract. (Abstraction)"
- Services/ScoringService.cs â€” "Scores an attempt. (Polymorphism)"
- Services/ITopicGenerationService.cs â€” "The AI topic contract. (Abstraction)"
- Services/TopicGenerationModels.cs â€” "Data shapes for AI topics."
- Services/TopicGenerationService.cs â€” "Asks the AI for a new topic."
- Services/UpdateCheckService.cs â€” "Checks GitHub for a newer version."
- Services/SelfUpdateService.cs â€” "Downloads and installs the update."
- Services/SessionStore.cs â€” "Remembers the player's name between launches."
- UI/AchievementsForm.cs â€” "The achievements screen."
- UI/AnswerTile.cs â€” "A clickable answer tile."
- UI/AttemptReviewForm.cs â€” "Shows the answers of a finished attempt."
- UI/CardPanel.cs â€” "A rounded card. (Inheritance)"
- UI/DialogForm.cs â€” "The app's message dialog. (Inheritance)"
- UI/FlatButton.cs â€” "A styled button. (Inheritance)"
- UI/GenerateQuizForm.cs â€” "The AI quiz creation screen."
- UI/HistoryForm.cs â€” "The past attempts screen."
- UI/IModulePanel.cs â€” "The contract every screen follows. (Abstraction)"
- UI/InputArea.cs â€” "The question input area."
- UI/InputField.cs â€” "A text field with an error line."
- UI/IosListGrid.cs â€” "The card list used by the screens."
- UI/LevelChip.cs â€” "The level badge in the sidebar."
- UI/LiveStatusLabel.cs â€” "The status line under the header."
- UI/LoginForm.cs â€” "The name screen. (Inheritance)"
- UI/MainForm.cs â€” "The main window with the sidebar. (Inheritance)"
- UI/MarkedCheckBox.cs â€” "A checkbox with a mark."
- UI/NavItemButton.cs â€” "A sidebar button."
- UI/ProgressTrack.cs â€” "The progress bar."
- UI/QuizCard.cs â€” "One quiz row card."
- UI/QuizListFilter.cs â€” "The filter row of the quiz list."
- UI/QuizTakeForm.cs â€” "The quiz-taking screen."
- UI/ReportsForm.cs â€” "The reports screen."
- UI/ScoreboardForm.cs â€” "The leaderboard screen."
- UI/SegmentedControl.cs â€” "The two-tab switch control."
- UI/StatusBadge.cs â€” "A small status label."
- UI/ThemedComboBox.cs â€” "A styled dropdown."
- UI/TopicManageForm.cs â€” "The topics screen."
- UI/UiFormat.cs â€” "Small text formatting helpers."
- UI/UITheme.cs â€” "All colors, fonts and sizes. (Encapsulation)"
- UI/UpdateDialog.cs â€” "The update dialog."
- UI/UpdateProgressDialog.cs â€” "The updating dialog."

## Verification

- `dotnet build` (Debug + Release) 0 warnings 0 errors; full suite 175/175 â€”
  comments only, zero code changes beyond deleted comment lines.
- grep: no `///` left in src; every file starts with exactly one `//` line.
- Pillar tags cross-checked against docs/oop-explanation.md (which stays the
  detailed reference and is unchanged).

---

---

# Task 5 (2026-11-12, active): campaign levels + AI rival + run game-feel

Owner directives (binding): auto-advance after answering; progressive
difficulty Easy -> Medium -> Hard -> **Insane** (new tier); sprites on the
run screen (assets embedded: Assets/Sprites/correct.png + wrong.png);
replaying any level shuffles question + answer order (never constant);
**campaign of 25 built-in levels replacing the seeded demo quizzes**, and
when the highest level is completed the AI acts as the ENEMY: a dialog where
it says it will add another quiz for you, then generates the next level;
reviewer pass after every finished increment; version 1.4.0; implementer
builds, Lead plans/integrates.

Decisions: Campaign topic (Programming) with quizzes `Level N` (1..25),
5 questions each, Approved; ladder L1-5 Easy, L6-12 Medium, L13-19 Hard,
L20-25 Insane; XP weights 10/20/30/40. Seed.sql replaces the demo quizzes
(users/attempts stay); migrator ensures the campaign on old databases
(idempotent) and leaves legacy quizzes in place. Insane plumbing: enum +
weights + quizzes CHECK rebuild (PRAGMA foreign_keys=OFF + legacy_alter_
table=ON outside the transaction, foreign_key_check after) + prompt/parser
+ Generate/Reports + chip tokens (InsaneText #D8B4FE / InsaneTint #3B2154).
questions.difficulty column (ALTER ADD + backfill from the parent quiz);
mix copies stamp source difficulty and persist progressive positions.
Run order: Quiz.OrderQuestionsForRun(Random) groups by question.Difficulty
ascending, shuffles within tiers; Question.ShuffleChoices(Random) shuffles
answers. Rival flow: completing the CURRENT HIGHEST Level N (>= 25) opens
the rival dialog -> AI generates Level N+1 (Insane, 5 questions, Campaign
topic) via the existing chain -> Play now / Later; busy + retry inside the
dialog, failure never blocks.

| # | Criterion |
|---|---|
| C1 | Fresh install: Take quiz lists exactly Level 1...25, no old demo quizzes. |
| C2 | Auto-advance works (correct ~1.2 s, wrong ~1.8 s, last auto-submits, manual navigation cancels). |
| C3 | Replaying shuffles question AND answer order. |
| C4 | Sprites appear in the feedback row; glyph fallback without them. |
| C5 | Highest-level completion opens the rival dialog; accept generates Level N+1 (Insane) with Play now; retry on failure. |
| C6 | Insane: XP 40/correct, chips, AI generation works, legacy DBs migrate intact. |
| C7 | Reviewer pass over the task-5 diff; findings fixed or dispositioned. |

WPs (one Implementer, sequential): WP1 insane plumbing; WP2 campaign seed
(25 levels / 125 questions) + ensure-campaign; WP3 run UX (auto-advance,
shuffle, sprites, rival dialog); WP4 QA + Reviewer + fixes.

---
# Task 4 (2026-11-11, active): random mix quizzes + game-like quiz screen

**Owner requests:** (1) quizzes must be "random, not static"; (2) the
quiz-taking screen design is bad â€” make it more game-like; (3) owner approves
downloading assets from GitHub for the redesign.

**Owner answers (binding):**
- Random = **"Random mix" quiz**: a fresh quiz built by drawing random
  questions from ALL saved topics, answers shuffled; instant, offline.
- Design direction = **more game-like** (verdict colors, icons on answer
  tiles, progress celebration, playful).
- Assets = **Fluent UI icon set** (free, MIT, from microsoft/fluentui-system-icons).

**Decisions (Lead, from the schema evidence):**
- `quiz_attempts.quiz_id` is NOT NULL and `quizzes.topic_id` is NOT NULL â†’
  each random mix is PERSISTED as a real quiz: a dedicated **"Random Mix"**
  topic (created idempotently on first use, category Programming) holds one
  timestamped quiz per take; the mix COPIES the drawn questions + choices as
  new rows under it (shuffled positions, shuffled choice order). Everything
  downstream (review, history, XP via Medium weight, achievements) then works
  unchanged. Draws only from APPROVED quizzes across ALL topics; the take
  button is disabled with a hint when fewer than 4 drawable questions exist.
- Verified reward path: AI-generated quizzes already earn XP + achievements
  (same Submit path; quiz-agnostic) â€” owner question answered, no change.
- Fluent icons: ship `FluentSystemIcons-Regular.ttf` next to the exe
  (`Assets/`), loaded via PrivateFontCollection with Segoe MDL2 fallback;
  installer iss + csproj include it.

## Work packages

| WP | Scope | Owner | State |
|---|---|---|---|
| T4-WP1 | Designer spec: game-like take-quiz screen + random-mix entry + result celebration | UX/UI Designer | âœ… spec delivered; Lead rulings: "Mix" chip approved; levelGained = pre-submit level read at StartQuiz; [Option] polish omitted |
| T4-WP2 | Random mix: service (draw/shuffle/copy, collision-safe title, dedicated topic) + Take-quiz banner entry + attempt flow | Lead | âœ… 8/8 service tests |
| T4-WP3 | Game redesign: Fluent icon font (embedded), token additions (StreakFlame/PerfectGold/QuestionHeadline/GameGlyph*), AnswerTile 5-state table, segmented progress, streak cue + single 180ms pulse, verdict bands + level-up band, result-row glyphs | Lead | âœ… code complete; visual walk-through pending (no renderer) |
| T4-WP4 | Integration: build, tests, review, visual walk-through | Lead | ðŸ”„ build 0/0 + 187/187 (Debug+Release); QA/review next |

## Integration notes (task 4)

- The Fluent font ships EMBEDDED in the exe (no loose file, no installer
  change); missing-font fallback = the pre-T4 Segoe glyphs.
- The owner's installed app gets the new build by direct exe swap; the
  official installer for Downloads comes from the background CI download.

---

# Task 2 (2026-11-11): in-app update flow + remembered name

**Owner request:** (1) updating must not redirect to GitHub â€” the app shows an
"Updatingâ€¦" dialog and performs the update itself; (2) the name screen asks
every launch â€” the name must be asked only once and remembered after the user
has entered it.

## Goal and acceptance criteria

| # | Criterion |
|---|---|
| B1 | The update dialog's primary button ("Update now") never opens a browser when the release carries an installer asset: clicking it shows an "Updating CodeQuest" dialog (download %, then verifying, then installing); when it finishes the app closes, the new installer runs silently, and CodeQuest relaunches by itself. |
| B2 | Download or integrity failure shows a friendly inline message in the same dialog with "Try again"/"Close"; "Close" (and "Cancel" during download) returns to the running session â€” the update never blocks studying. "Not now" keeps working. |
| B3 | A release **without** an installer asset falls back to the previous browser behavior (documented). |
| B4 | After one successful name sign-in, later launches go straight into the app (no name screen). "Log out" returns to the name screen (prefilled with the last name) and the next launch asks again. A saved name that no longer resolves in the database falls back to the name screen. |

**Assumptions (stated, low-impact):** the in-app update downloads the
release's installer asset (`CodeQuest-Setup*.exe`) and runs it
`/VERYSILENT /NORESTART` â€” the Inno script keeps `codequest.db` +
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
  `apply-update.cmd` (wait 2 s â†’ run installer `/VERYSILENT /NORESTART` â†’
  relaunch `AppContext.BaseDirectory\CodeQuest.exe`) and start it windowless.
  Every failure returns a typed outcome; nothing throws to the UI.
- **`UI/UpdateProgressDialog`**: "Updating CodeQuest" card (UpdateDialog
  family style) â€” status line + ProgressTrack + Cancel / (on failure)
  Try again + Close. DialogResult.OK only when the installer was launched.
- **`UI/UpdateDialog`**: button text "Update now"; click no longer opens the
  browser when an installer asset exists (browser fallback only for B3).
- **`UI/MainForm.MaybeShowUpdateDialog`**: offer â†’ progress dialog â†’ close
  shell only when the update is actually applying.
- **Remembered name:** new `Services/SessionStore` (instance, injectable
  directory for tests) persisting the last signed-in username at
  `%LOCALAPPDATA%\CodeQuest\session.json`; never throws. `Program.Main`
  auto-resolves the saved name through `UserRepository` (DB failure keeps the
  existing Â§4.11 behavior), skips the login on success, saves on sign-in, and
  clears on logout (`ReturnedToLogin`); `LoginForm` gains an optional
  prefilled name.
- **Docs:** `docs/release-process.md` re-issued: a release must attach
  `CodeQuest-Setup.exe` (installer) for in-app updates; describes the new
  update flow.

## Work packages (all Lead-direct: tightly coupled UI+Program changes, single writer)

| WP | Scope | Files | State |
|---|---|---|---|
| T2-WP1 | Installer asset selection + tests | `UpdateCheckService.cs`, `UpdateCheckTests.cs` | âœ… 13/13 update tests |
| T2-WP2 | Download/verify/apply service + tests | `SelfUpdateService.cs` (new), `SelfUpdateServiceTests.cs` (new) | âœ… 5/5 |
| T2-WP3 | Update progress dialog + dialog wiring | `UpdateProgressDialog.cs` (new), `UpdateDialog.cs`, `MainForm.cs` | âœ… |
| T2-WP4 | Remembered name | `SessionStore.cs` (new), `SessionStoreTests.cs` (new), `Program.cs`, `LoginForm.cs` | âœ… 8/8 (camelCase fix applied) |
| T2-WP5 | Docs + integration | `docs/release-process.md`, `docs/security-notes.md`, `docs/user-guide.md` | âœ… |
| T2-WP6 | Independent review (download-and-execute path) | â€” | âœ… Both reviewers returned. Security: findings F1â€“F7/P1â€“P8 fixed in `a470018`/`55fe240` (trusted-host allowlist, size cap, malformed-digest abort, Wait-Process on real exit, dialog-close cancels, timeoutâ‰ cancel, broad startup catch, https fallback, CodeQuest-Setup asset prefix); accepted risks recorded in docs/security-notes.md (unsigned installer, same-user temp tampering, shared-seat identity). Code review: M1 broad catch âœ“, m1 cancel+exit-code gate âœ“, m2 Start() check âœ“, m3 asset-parse robustness âœ“; end-to-end silent install still needs the owner's manual walk-through on a real update. |

## Integration status (2026-11-11)

- Debug + Release builds: 0 errors; full suite 179/179 (was 156 before task 2).
- Task 2 + task 3 + hardening committed; pushed on the owner's explicit
  instruction.
- The owner's installed app was upgraded directly to `1.2.0+55fe240`
  (exe swap; config now OpenRouter-only) â€” CI artifact download kept
  stalling, so the installer copy for Downloads comes from the resumed
  background download of run 55fe240's artifact.
- Generation-banner defect found via the owner's screenshot: a skipped tier
  (nvidia, empty key) masked openrouter's real result â€” DescribeFailure now
  describes the first REAL failure; configs carry openrouter only.
- Not done (owner-authorized only): version bump beyond 1.2.0, tag, GitHub
  release.

## Task 3 status (one-line comments): DONE

- 88 source files swept: one plain-language line per file (OOP pillar tags
  only where a pillar lives), all banners/inline/`///` comments removed,
  short CRUD lines in the repositories; tests untouched.
- Verified: `///` count 0, banner count 0, first-line/blank-line structure
  check clean across all 88 files, URLs in strings intact, Debug + Release
  builds 0 errors, 175/175 tests.
- Tooling: throwaway stripper at
  `%TEMP%\opencode\comment-stripper` (string-aware mini-lexer; interpolated
  raw-string handling added after the first run left two files unstripped â€”
  caught by review greps, sources restored from git, re-run clean).

---

# Task 1 (2026-11-08, shipped as 1.1.0): AI topics, gamification, dark theme

**Requested by owner (this task):** (1) adding a new topic = describe it, AI generates it;
(2) level + scoreboard for fun; (3) achievements section the app records;
(4) modern dark theme â€” mind shadow/border/typography/spacing, "no AI slop".
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
| A1 | Topics â†’ New topic: user types a short description, "Generate with AI" (off-UI-thread, busy state) produces name/category/description as an editable preview; Save runs the existing duplicate-name validation. AI failure shows a typed failure line; fields stay manually editable. |
| A2 | Completing a quiz awards XP = correct answers Ã— difficulty weight (Easy 10 / Medium 20 / Hard 30); the result screen shows XP earned, level progress, and any newly unlocked achievements. |
| A3 | XP/level are derivable from stored attempts (no redundant XP column); level thresholds: cumulative XP to reach level n = 25(nâˆ’1)(n+2) â†’ L2@100, L3@250, L4@450, L5@700, L6@1000. |
| A4 | New "Scoreboard" module ranks **all accounts** by total XP (rank, player, level, XP, quizzes taken), highlights the signed-in row, and shows the signed-in user's level + progress. |
| A5 | New "Achievements" module shows the catalog with locked/unlocked state and unlock dates; unlocks are recorded in the `user_achievements` table (PK (user_id, achievement_key)); re-completing never duplicates. |
| A6 | Whole app renders in the dark theme (re-issued token values); no light-mode remnants (checked by paint audit); cards gain the hairline-border elevation treatment; dialogs get a subtle native drop shadow; design spec Â§2 re-issued. |
| A7 | `database/schema.sql` gains `user_achievements`; an idempotent startup migration adds it to **existing** databases without touching existing rows (attempts must survive â€” they feed the scoreboard). |
| A8 | New unit tests pass (XP/level math, achievement evaluation incl. idempotence, topic JSON strict parser incl. category enum, leaderboard aggregation, migration idempotence); full suite green; build clean. |

**Non-goals:** light/dark toggle, account registration UI, streaks/daily bonuses, cloud sync,
XP write-back into `quiz_attempts` (XP stays derived), per-question live XP animation,
changes to the quiz generation contract (`contracts/ai-quiz-api.md` semantics preserved).

## 2. Confirmed findings (evidence)

- **Stack:** WinForms, `net10.0-windows`, SQLite (`Microsoft.Data.Sqlite`), xUnit tests,
  solution `CodeQuest.sln`; version 1.0.4 in `src/CodeQuest/CodeQuest.csproj`. Working tree clean.
- **Design system:** `UITheme.cs` is the single token source (spec Â§2 discipline: no hard-coded
  colors in forms). Re-issuing token values re-skins the app; painters must be audited for
  light assumptions (`ControlPaint.Dark`, white literals, `SystemColors`).
- **`CardPanel`** (recipe 4.2) has **no border/shadow** â€” light theme separated white cards from
  `#F5F5F7` by contrast alone. Dark needs the Â§3.6 "borders" treatment: 1px hairline on cards.
- **DB:** `Db` never creates tables; `Program.cs` blocks when the DB file is missing. Existing
  `codequest.db` holds attempts â†’ new table must come from an idempotent `CREATE TABLE IF NOT
  EXISTS` migration at startup, plus `schema.sql` for fresh installs.
- **XP derivation:** `quiz_attempts.score` = correct count; difficulty on `quizzes`; users on
  `users.display_name`. Leaderboard = JOIN + `SUM(score Ã— multiplier)`; nothing stored (consistent
  with the plan's normalization rulings, e.g. R-2 #1).
- **AI pattern to reuse:** `ChatCompletionsClient` (one wire client, per-provider parameters),
  `QuizGenerationService` (chain: config order, skip rules, first success wins, typed failures,
  never throws to UI), `StrictQuizJsonParser` (fence-stripping, whole-object JSON, field rules),
  `GenerateQuizForm` (off-UI-thread `Task.Run` + `ContinueWith` + busy state + typed banner).
- **Topic flow today:** `TopicManageForm` dialog = Name + 2-value Category combo
  (`Programming`/`Cybersecurity`, schema CHECK); description column exists but the form saves
  `string.Empty`; duplicate name is validated inline (R-2 #5) and caught as
  `DuplicateEntityException`.
- **Attempt write point:** `QuizTakeForm.Submit()` â€” one `AddWithAnswers` write, then result view.
  This is where XP/achievement evaluation hooks.
- **Nav:** `MainForm.Modules` currently has 5 entries (TakeQuiz, History, Reports, GenerateQuiz,
  Topics); module panels persist, reload data on show (`IModulePanel.RefreshData`).
- **Seed data:** 1 user, 5 topics, 5 quizzes, 4 attempts â€” enough to demo the scoreboard.

## 3. Requirements (functional)

- **FR1 â€” AI topic creation (preview then confirm):** In the New-topic dialog: description field
  (required, 5â€“300 chars) + "Generate with AI" primary button. Generation walks the same provider
  chain; strict JSON response `{name (1â€“60 chars), category ("Programming"|"Cybersecurity"),
  description (10â€“200 chars)}`. Result fills Name/Category/Description fields, all editable
  (manual fallback when AI is down). Save keeps the existing duplicate/empty validation.
  Edit-topic flow unchanged (manual).
- **FR2 â€” XP and levels:** XP awarded on attempt submit = correct Ã— weight(Easy 10/Medium 20/
  Hard 30). Levels from cumulative thresholds 25(nâˆ’1)(n+2). Level visible in the sidebar account
  block (chip + progress), on the Scoreboard, and on the result screen.
- **FR3 â€” Scoreboard module:** leaderboard over **all users**: rank, display name, level, total
  XP, quizzes taken â€” ordered by XP desc (tie: more quizzes, then name). Signed-in row visually
  highlighted. A personal summary card (level, XP, progress to next level) sits above the table.
- **FR4 â€” Achievements module:** code-defined catalog (key, title, description, glyph, rule):
  first-quiz, perfect-run (100% any), perfect-hard (100% Hard), 10-quizzes, 30-quizzes,
  50-correct, level-5, level-10, 3-distinct-topics, both-categories. Evaluated after each attempt
  submit; unlocked rows recorded in `user_achievements` with timestamp. UI: card grid, locked
  (muted) vs unlocked (accent/semantic) with unlock date.
- **FR5 â€” Dark theme (dark only):** re-issue every token (tables below); keep the 8px grid,
  Segoe UI scale, 12/8/4 radii (typography/spacing discipline is *not* changed); cards gain 1px
  `BorderHairline`; dialogs gain `CS_DROPSHADOW`; all painters audited; `designs/codequest-ui-spec.md`
  Â§2 re-issued (the "dark mode is out of scope" note is superseded by owner direction).

## 4. Dark token starting point (SUPERSEDED by the validated D4 amendment)

> **Supersession note (2026-11-08, post WP2):** the UX/UI Designer computed real WCAG ratios and
> re-issued several starting values below. The **binding** palette is now
> `designs/d4-dark-theme-amendment.md` Â§1 (applied to `designs/codequest-ui-spec.md` by WP3).
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
| Input borders (â‰¥3:1 on Surface) | `BorderControl` | `#4A5264` |
| Card hairline (new) / separators | `BorderHairline` / `SeparatorHairline` | `#262B36` |
| Text primary/secondary/tertiary | | `#E8EAF0` / `#9BA3B4` / `#6B7385` |
| Accent fill (white label 4.8:1) | `Accent`/`Hover`/`Pressed`/`Disabled` | `#6C5CE7` / `#5A4BD1` / `#493BB8` / `#3F3B5E` |
| Accent as text on dark (â‰¥4.5:1) | `AccentText` (new) | `#A79BF5` |
| Selected nav bg | `AccentTint` / `NavItemPressed` | `#2A2749` / `#262244` |
| Success fill/text/tint | | `#2FA857` / `#4CC27A` / `#16301F` |
| Danger fill/text/tint | | `#C62B25` / `#F07A72` / `#3A1A18` |
| Warning fill/text/tint | | `#B07C10` / `#E5A83B` / `#38290F` |
| Tiles / difficulty colors | re-tuned for dark, white large text â‰¥3:1 | Red `#E14B5A` Â· Blue `#3D7BF0` Â· Amber `#E09413` Â· Green `#27A45C` |

Rules: AA for all text (4.5:1; large/bold 3:1), non-text borders â‰¥3:1; state never color alone;
accent only on primary action/current selection; tints become dark washes; **no gradients, no
glow, no decorative color** â€” restraint is the anti-slop bar.

## 5. Architecture decisions

- **New tables:** only `user_achievements (user_id, achievement_key, unlocked_at)`,
  PK (user_id, achievement_key), FK â†’ users ON DELETE CASCADE. XP/levels/leaderboard derived.
- **Migration:** `Data/DatabaseMigrator.EnsureSchema(Db)` â€” idempotent DDL, called in `Program.cs`
  right after the DB-file check; never touches data; safe to run every launch.
- **Services (all behind interfaces, matching the codebase pattern):**
  - `ILevelService` + `LevelService` (pure math: XPâ†’level, progress; difficulty weights).
  - `IGamificationRepository` + `GamificationRepository` (leaderboard rows, per-user XP/quizzes
    aggregate, achievement get/add-if-new). `AttemptRepository` untouched.
  - `IAchievementEvaluator` + `AchievementEvaluator` (catalog in code; input = user aggregates +
    level; output = newly unlocked keys).
  - `ITopicGenerationService` + `TopicGenerationService` (chain semantics copied from
    `QuizGenerationService`) + strict `TopicDraftJsonParser`.
- **UI:** new `ScoreboardForm`, `AchievementsForm` (`ModulePanel` subclasses, persistent panels,
  reload on show); `MainForm` gains two nav entries + sidebar level chip; `TopicFormDialog`
  reworked for describeâ†’generateâ†’preview; `QuizTakeForm` result view gains XP/level/achievement
  reveal. All colors/fonts/spacing via `UITheme` only.
- **Why not store XP:** attempts are the source of truth; deriving avoids drift and keeps the
  3NF story intact (normalization docs).

## 6. Work packages (order, owners, dependencies)

| WP | Owner | Scope (files) | Depends on |
|---|---|---|---|
| WP1 | Implementer | Gamification foundation: `Models/Gamification.cs` (records), `Data/IGamificationRepository.cs` + `Data/Repositories/GamificationRepository.cs`, `Data/DatabaseMigrator.cs`, `Services/LevelService.cs`, `Services/AchievementEvaluator.cs` (+ `IAchievementEvaluator`), `Program.cs` hook, `database/schema.sql`, tests (`LevelServiceTests`, `AchievementEvaluatorTests`, `GamificationRepositoryTests`, `DatabaseMigratorTests`) | â€” |
| WP2 | UX/UI Designer | Read-only spec: dark token re-issue (Â§2 amendment with contrast math), Scoreboard/Achievements/New-topic-dialog/level-chip/result-reveal screen specs as recipes in the existing spec format | â€” |
| WP3 | Implementer | Dark theme application: `UITheme.cs` re-issue + `CardPanel` hairline border + dialog `CS_DROPSHADOW` + painter audit across **existing** UI files + `designs/codequest-ui-spec.md` Â§2 update | WP2 |
| WP4 | Implementer | AI topic generation: `Services/TopicGenerationService.cs` (+ interface, parser, outcome types), `UI/TopicManageForm.cs` dialog rework, `MainForm` wiring of the service, tests (`TopicGenerationTests`) | WP2 (dialog recipe), WP3 (dark tokens in place) |
| WP5 | Implementer | Gamification UI: `UI/ScoreboardForm.cs`, `UI/AchievementsForm.cs`, `MainForm` nav + sidebar level chip, `QuizTakeForm` result XP/level/achievement reveal, tests where feasible | WP1, WP2, WP3 |
| WP6 | Lead | Integration: build, full test run, polish pass | WP1â€“WP5 |
| WP7 | QA | Functional verification against A1â€“A8 | WP6 |
| WP8 | Reviewer | Independent diff review (before any version bump/release; owner directive) | WP7 |

File ownership: WP4 owns `TopicManageForm.cs`; WP5 owns `MainForm.cs` + `QuizTakeForm.cs` + the
two new screens; WP3 touches existing painters but completes **before** WP4/WP5 start.
**Scheduling correction (2026-11-08):** WP4 and WP5 run **sequentially** (WP4 first), not in
parallel: both would need solution builds/tests, and two concurrent `dotnet build` runs share
the same `obj/`/`bin/` outputs â€” the source write scopes are disjoint, the build artifacts are
not. Any new control painters must use tokens only (a short audit of the new files is folded
into WP6).

## 7. Risks

- **Dark paint audit completeness** â€” resolved by WP3's per-file audit table (28 files, 0 errors,
  135/135 tests); QA (WP7) still does the visual A6 walk-through â€” no screenshot verification was
  possible during WP3.
- **Contrast regressions** â€” the D4 amendment's computed ratios are the reference; QA spot-checks.
- **Migration on existing DB** â€” idempotent `CREATE TABLE IF NOT EXISTS`; migrator tests cover
  re-run and data preservation; verified by WP1 tests.
- **AI output quality for topics** â€” strict parser + preview-then-confirm (owner's choice) guards it.
- **WinForms thread rules** â€” generation follows the proven `GenerateQuizForm` pattern.

### Found defects and rulings (log)

- **Review WP8 (independent, 2026-11-08): verdict DO-NOT-SHIP â†’ both findings fixed same day.**
  - **MAJOR-1 (fixed):** `GamificationRepository` compared the perfect-run rule against the
    *answered* count â€” a partial attempt (1 correct of 5, questions skipped â€” the player allows
    it) permanently unlocked "Flawless"/"Nightmare Slayer" (the ledger never revises a first
    unlock). The rule now requires `answered == quiz question count && score == question count`
    (the query joins the quiz's question total); regression test
    `GetUserStats_PartialAttemptWithSkippedQuestions_IsNeverPerfect` added.
  - **MINOR-1 (fixed):** the achievement catalog carried emoji glyphs, painted with Segoe MDL2
    Assets (no emoji â†’ tofu/colour fallback, breaking the no-decorative-colour rule). All ten
    replaced with the Â§5.14 verified codepoints (E768, E73E, E930, E7BC, E8F1, E8FB, E870,
    E945, E81E, EA86).
  - Areas reviewed clean: migration idempotence/FK, leaderboard aggregation, add-if-new
    semantics, threading (topic dialog has stronger guards than its quiz twin), secrets, UI
    wiring, dark re-issue regressions, test quality (one gap â€” the skipped-questions case â€”
    now closed).
  - **Delta review verdict: SHIP** for 1.0.4 â†’ 1.1.0 (both fixes confirmed complete and
    correct, incl. an independent re-check of the rule against the seeded data). Remaining:
    the owner's visual spot-check of the MDL2 achievement icons (the only item a human eye
    must confirm) and the release authorization.
- **Pre-existing defect (found in WP3 audit, confirmed against HEAD): FIXED 2026-11-08 (Lead).**
  `QuizTakeForm.HandleTileClick` guarded on `tile.Tag`, which `RenderQuestion` never assigned â€”
  every mouse tile click no-op'd. The guard now reads `AnswerTile.ChoiceId` (the tile's own
  property, set at render). QA must verify click + keyboard selection end-to-end on the run.
- **Lead ruling (WP3 deviation #6):** `UITheme.Display` (28 pt bold) stays as the result-score
  font (owner fun-UI directive supersedes the amendment's `Title` restatement for that one
  element; recorded in the D4 amendment Â§2).
- **WP6 polish candidates (accepted, non-blocking):** History/Reports/GenerateQuiz use plain
  square `Surface` panels without the D4 card hairline â€” unify on `CardPanel` during WP6 if
  cheap; ThemedComboBox native dropdown chrome needs a QA visual check on dark.

### Progress log

- WP1 âœ… (Implementer, Lead-verified: code read, tests re-run).
- WP2 âœ… (Designer; amendment saved to designs/d4-dark-theme-amendment.md; Lead decisions bound).
- WP3 âœ… (Implementer; 0 warnings, 28-file audit table; Lead accepted deviations).
- WP4 âœ… (Implementer phases 1/3/4; Lead completed phase 2 â€” deleted the leftover
  TempProbeTests.cs, wrote TopicGenerationTests.cs (20 cases: valid/fenced output, every
  strict-parser violation, wire-class passthrough, chain skip/order/terminal rules, request
  guard) â€” one test expectation corrected against the contract grammar ("nvidia:quota:429").
  Build 0/0; 155/155 tests at that point).
- WP5 âœ… (Lead, parallel where independent): LevelChip.cs, ScoreboardForm.cs,
  AchievementsForm.cs, MainForm integration (Â§5.2a nav, Â§4.14 sidebar level block,
  UpdateSidebarLevel), tile-click defect fix, and the Â§5.7a result reveal (XP line + level row
  + conditional achievement chips, hero card grows to fit, post-submit sidebar refresh,
  graceful degradation when progress recording fails). Build 0 warnings / 0 errors; final
  full-suite run in flight at the time of writing.
- WP6 (Lead integration): pending the final suite result; then WP6 polish candidates if cheap.
- Next: WP7 QA (functional walk-through vs A1â€“A8) and WP8 Reviewer (independent diff review â€”
  the owner directive: reviewer BEFORE any version bump/release).

## 8. Verification plan (A# â†’ check)

- A1/A2/A4/A5: QA manual scenarios + unit tests below.
- A3: `LevelServiceTests` (threshold boundaries 99/100/249/250â€¦, level 1 at 0 XP).
- A5: `AchievementEvaluatorTests` (each rule fires / doesn't; idempotent re-run),
  `GamificationRepositoryTests` (add-if-new, dates).
- A6: WP3 audit checklist + designer review of implemented screens.
- A7: `DatabaseMigratorTests` (fresh DB, existing DB re-run, rows preserved); manual: launch on a
  copy of the real `codequest.db`.
- A8: `dotnet build CodeQuest.sln` + `dotnet test CodeQuest.sln` â€” both green before completion.

**Task 5 COMPLETE (2026-11-12).** Final state at `55820ea` (CI green): campaign.sql =
exactly Level 1 (Easy, 5 questions, 20 choices, idempotent); ladder via
`CampaignLevelService` (2-5 Easy / 6-12 Medium / 13-19 Hard / 20+ Insane, service-level
test proves the request difficulty is overridden); rival copy + a caption under the
campaign card ("Complete the level and the AI generates the next one."); app icon
verified end-to-end via ExtractAssociatedIcon on the built exe. Legacy databases KEEP
their old levels (played content is never deleted); fresh installs get only Level 1.
Deviations (accepted): ladder stamping lives in CampaignLevelService, not the UI; success
copy names the tier; user-guide updated beyond the requested copy points. 236/236 tests,
builds 0/0, real-DB migration harness clean (legacy rows intact, foreign_key_check 0).
