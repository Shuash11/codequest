<!-- CodeQuest — release process (increment I6+) -->

# Release Process

How to publish a new version so downloaded copies see the **"Update required"**
dialog with your release notes — and so installed copies can **update inside
the app** (task 2): "Update now" downloads your installer, verifies it, runs
it silently, and relaunches CodeQuest. The user never visits GitHub.

## The version chain

| Where | What | Notes |
|---|---|---|
| `src/CodeQuest/CodeQuest.csproj` | `<Version>1.0.0</Version>` | What the running app reports — **bump this first** |
| GitHub release tag | `v1.1.0` (leading `v`) | What the update check compares against |
| Release **body** | plain markdown list | What the update dialog shows as "What's new" |
| Release **asset** | `CodeQuest-Setup.exe` | What the in-app update downloads and runs silently — **required for the in-app update** |
| Release **asset** | `CodeQuest-win-x64.zip` | The browser fallback for very old copies; optional |

The release page for each new version must carry the **installer** built by
CI (`CodeQuest-Setup-*` artifact, renamed to the unversioned
`CodeQuest-Setup.exe` so links keep working). The installer is what makes the
in-app update safe for user data: its `onlyifdoesntexist` flags keep
`codequest.db` and `config.local.json` across the upgrade. A release without
the installer still shows the dialog, but falls back to the old browser
hand-off.

## Steps

1. **Bump the version** in `src/CodeQuest/CodeQuest.csproj` (`Version`,
   `AssemblyVersion`, `FileVersion`) and commit.
2. **Push to `main`** — CI builds, tests, and publishes the artifacts
   (`CodeQuest.exe` + seeded `codequest.db` + `config/config.local.json` with
   the AI key from the repository secret, plus the Inno Setup installer).
3. **Download both artifacts** from the green run:
   the `CodeQuest-Setup-*` installer (rename to `CodeQuest-Setup.exe`) and
   the `CodeQuest-win-x64` artifact zipped as `CodeQuest-win-x64.zip`
   (unversioned names — the website links keep working).
4. **Create the release** on the public `Shuash11/codequest` repository:
   ```bash
   gh release create vX.Y.Z CodeQuest-Setup.exe CodeQuest-win-x64.zip ^
     --title "CodeQuest X.Y.Z" ^
     --notes "- What changed for users, one bullet per change"
   ```
   The release notes are what the update dialog shows — write them for the
   *user*, not the developer ("Quizzes now remember your answers", not
   "refactored X").
5. **Push the matching tag** to the private source repository too
   (`git tag vX.Y.Z && git push origin vX.Y.Z`) so the source history matches
   the release.

## How the check works (for the code review)

- Once per launch, after login, the app calls GitHub's public
  `releases/latest` API for `Shuash11/codequest` (no authentication, no data
  sent, 8-second timeout).
- The release tag is compared numerically against the running version.
- A newer release opens the **"Update required"** dialog: the new version,
  the current one, and the cleaned-up release notes. "Update now" performs
  the update **inside the app** (`UpdateProgressDialog` + `SelfUpdateService`):
  the `CodeQuest-Setup.exe` asset is downloaded with a progress dialog and a
  cancel button, checked against GitHub's sha256 digest when one is supplied,
  and then run silently (`/VERYSILENT /NORESTART`) by a tiny apply script that
  relaunches CodeQuest when the install finishes. The app closes itself for
  the install. A download or integrity failure shows a friendly message with
  "Try again"/"Close" and keeps the session; "Not now" also keeps it.
- A release **without** an installer asset falls back to opening the release
  asset in the browser (the pre-task-2 behavior).
- Every check failure (offline, rate limit, timeout, malformed payload) is
  silent — the check never blocks or crashes a study session.
