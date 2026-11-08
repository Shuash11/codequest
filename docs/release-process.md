<!-- CodeQuest — release process (increment I6+) -->

# Release Process

How to publish a new version so downloaded copies see the **"Update required"**
dialog with your release notes.

## The version chain

| Where | What | Notes |
|---|---|---|
| `src/CodeQuest/CodeQuest.csproj` | `<Version>1.0.0</Version>` | What the running app reports — **bump this first** |
| GitHub release tag | `v1.1.0` (leading `v`) | What the update check compares against |
| Release **body** | plain markdown list | What the update dialog shows as "What's new" |
| Release **asset** | `CodeQuest-win-x64.zip` | What the dialog's download button opens |

## Steps

1. **Bump the version** in `src/CodeQuest/CodeQuest.csproj` (`Version`,
   `AssemblyVersion`, `FileVersion`) and commit.
2. **Push to `main`** — CI builds, tests, and publishes the artifact
   (`CodeQuest.exe` + seeded `codequest.db` + `config/config.local.json` with
   the AI key from the repository secret).
3. **Download the `CodeQuest-win-x64` artifact** from the green run and zip it
   as `CodeQuest-win-x64.zip` (unversioned name — the website link keeps
   working).
4. **Create the release** on the public `Shuash11/codequest` repository:
   ```bash
   gh release create vX.Y.Z CodeQuest-win-x64.zip ^
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
  the current one, and the cleaned-up release notes. "Download update" opens
  the asset in the browser and closes the app; "Not now" continues the
  session.
- Every failure (offline, rate limit, timeout, malformed payload) is silent —
  the check never blocks or crashes a study session.
