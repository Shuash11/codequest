// Security notes — CodeQuest (final exam project)
// Written for increment I5 (the plan's security work, formerly phase P15).
// Last updated: 2026-11-08.

# Security Notes

## Scope and posture

CodeQuest is a single-user, local-desktop application: C#/.NET WinForms over a
local SQLite file. There is no network listener, no multi-user surface, and no
web tier. The threat model is therefore compact: protect the AI provider keys,
keep the local database honest (integrity, not confidentiality from the person
sitting at the machine), and never turn a failure into a crash or a leak.

## 1. AI provider keys

- Both provider keys (NVIDIA, OpenRouter) live **only** in
  `config/config.local.json`, which is git-ignored from the first commit
  (`.gitignore`). The committed `config/config.example.json` documents the
  shape without secrets.
- Keys are never hard-coded, never logged, and never committed. The key
  travels only inside the HTTP `Authorization` header of a provider request
  (`ChatCompletionsClient`); exception messages and the raw response body are
  key-free by construction (the raw body is provider output, not the request).
- The generation failure banner (spec 5.11) renders **plain-language**
  mappings of the typed failure classes — never the raw reason string, never
  a provider name, never configuration detail.
- History sweep: `git log -p` for the key-format patterns finds nothing (the
  keys were never pasted by any agent; the owner pastes them personally into
  the git-ignored file).
- **Distribution key injection (deliberate tradeoff, documented 2026-11-08):**
  the owner directed that the downloadable CI build work out of the box for a
  **single client** (the instructor). The CI workflow therefore writes
  `config/config.local.json` into the published artifact from the repository
  secret `OPENROUTER_API_KEY` — the key exists in the artifact, **by design**.
  This is acceptable *for this project's actual scope* and would not be for a
  real product: the repository is private (artifact access requires repo
  access), the audience is one person, the models are free-tier, and the
  quota is the owner's own 50 requests/day. The key never enters the
  repository, the source, or the build logs (GitHub masks secret values).
  **Consequences the owner accepted:** anyone holding the artifact holds the
  key (extractable from the config next to the exe); the free-tier quota is
  shared with the owner's own use; and if the key is ever leaked or abused it
  must be rotated in the GitHub secret and locally. **The production answer
  remains what it was:** each user brings their own free key
  (`docs/user-guide.md` §6) or a small server-side proxy holds a key
  server-side — never a client-side injection.
- **Disposition (D1, name-only sign-in):** the PBKDF2 password hasher
  delivered in the shell phase was removed together with its tests when the
  owner directed name-only sign-in. The `users.password_hash` column remains
  NOT NULL in the schema and is stored as an empty string for created
  accounts. This is a recorded, deliberate weakening: the app holds no
  secrets worth authenticating (local quiz data only), and the instructor-
  visible tradeoff — losing the PBKDF2 demonstration — was the owner's
  explicit call. Nothing else reads that column.

## 2. SQL injection

- Every SQL statement in the data layer is **constant text + parameters**
  (`AddWithValue("$name", …)`). The audit swept every `CommandText`
  assignment under `src/CodeQuest/Data/` for concatenation, string
  interpolation, or `string.Format`: **zero hits** (I5 audit, 2026-11-08).
- `PRAGMA foreign_keys = ON` runs on every connection (the connection
  factory), so referential integrity is enforced by the database, not by
  discipline.

## 3. Input validation and error handling

- Every input form declares its validation (required fields, lengths,
  numeric ranges, the exactly-one-correct-choice rule at persist time, and
  duplicate-title checks against the UNIQUE keys). Rejections show specific
  inline messages; nothing raw reaches the user.
- Unexpected exceptions surface through the application's single
  unexpected-error dialog (plain language, no stack trace) from
  `Program.cs`'s thread/domain handlers.
- **Local error log (I5):** unexpected exceptions append one line
  (UTC timestamp, exception type, message) to
  `%LOCALAPPDATA%\CodeQuest\error.log`. No key material can appear there
  (keys never enter exception messages); the log write is best-effort and
  can never itself crash the app.
- **Update check (I6+):** once per launch the app contacts GitHub's public
  releases API (`api.github.com/repos/Shuash11/codequest/releases/latest`,
  unauthenticated, 8-second timeout, no data sent beyond a User-Agent
  header) to detect a newer version. Every failure is silent.
- **In-app update (task 2, hardened after the 2026-11-11 security review):**
  when the release carries an installer asset, "Update now" downloads it over
  https from GitHub **only** (`github.com` / `*.githubusercontent.com` hosts,
  500 MB size cap, random per-attempt temp folder under
  `%TEMP%\CodeQuestUpdate`, cleaned up afterwards), verifies the sha256
  digest GitHub supplies for the asset when one is present. A **malformed**
  digest stops the update; an **absent** one proceeds unverified. The apply
  script (PowerShell, single-quoted paths, no user input) waits for the
  running app to actually exit, installs silently, relaunches, and deletes
  the installer and its folder. Closing the update dialog cancels the
  download; a network timeout is reported as a failure, not a cancellation.
- **Accepted risks (recorded, owner-informed):**
  - The installer is **not code-signed** (no certificate exists). The
    in-app path compensates with TLS + the GitHub-supplied sha256 digest +
    the pinned release repository; a signed release is the eventual fix.
  - A same-user process could tamper with the temp folder between verify
    and launch (per-user `%TEMP%`; no privilege boundary is crossed).
  - On a shared Windows account, the remembered name lands the next person
    in the previous profile; the name screen still gates nothing by design
    (directive D1: name-only sign-in).
- No other network calls exist besides the AI generation the user explicitly
  triggers.
- Grep for empty catch blocks: **clean** (the only swallow-by-design catch is
  the error-log write itself, commented as such).

## 4. Data minimization

- The only stored personal data is the **display name** the user types at
  sign-in (directive D1) — no email, no password, no usage telemetry, no
  network calls other than the AI generation the user explicitly triggers.
- The **remembered name** (task 2) is that same login name stored
  machine-locally in `%LOCALAPPDATA%\CodeQuest\session.json` so later
  launches skip the name screen; it never travels over the network and is
  cleared by "Log out".
- Quiz attempts store scores and chosen options; nothing about the machine
  or the user beyond the name and timestamps.

## 5. Threat model summary (local desktop app)

| Threat | Disposition |
|---|---|
| Key leakage via repo/logs/UI | Git-ignored config; log/UI paths audited key-free; `git log -p` clean |
| SQL injection | Constant-text parameterized commands only (audited) |
| Referential corruption | FK enforcement ON per connection; orphaned writes refused |
| Crash-on-failure | Typed generation outcomes; §4.11 dialog for the unexpected; no empty catches |
| Sensitive data at rest | Only a display name + quiz data on the local machine — nothing to protect from the machine's owner |
| Hostile input via keyboard | Every form validates; FK-protected deletes blocked with friendly messages |
