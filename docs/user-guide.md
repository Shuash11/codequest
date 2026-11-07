<!-- CodeQuest — user guide / demo script (increment I6 deliverable) -->

# CodeQuest — User Guide and Demo Script

This document doubles as the demo script: the steps below are the exact
clicks, in order, with the expected outcome of each. They assume a fresh
machine with the app running (from the CI-built `CodeQuest.exe` artifact, or
`dotnet run` on a machine with the .NET 10 SDK) and the database seeded
(`database/schema.sql` + `database/seed.sql` — the app blocks with a friendly
message if the database file is missing).

**The offline rehearsal (the safe demo path):** everything except
"Generate quiz" works with the network disconnected. Rehearse the full script
once online, then unplug and run it again — every step except step 6 must
behave identically.

## 1. Sign in

1. Launch the app. The login window shows "CodeQuest" and a single Name
   field.
2. Type a name (for example `Demo Student`) and press Enter (or "Sign in").
   - **Expected:** the main window opens; "Take quiz" is the active module;
     the sidebar shows your name under the nav list.
3. The first name creates the profile; the same name in a later session
   resolves to the same profile (attempts stay attributed).

## 2. Take a quiz (offline-capable)

1. In "Take quiz", the list shows the approved quizzes with Title, Topic,
   Difficulty, Questions, and an Approved badge. A hint under the list reads
   "Enter activates a focused row action."
2. Press "Start" on **C# Core Concepts** (or click the row's action).
   - **Expected:** the run header shows the quiz title and "Question 1 of 5";
     an 8px progress track; the question card with four choice rows.
3. Click a choice — the row tints and its mark fills (one selection; click
   another to move it).
4. Use **Next** / **Previous** to navigate — selections survive moving back
   and forth. On the last question the button reads **Submit**.
5. Press **Submit**.
   - **Expected:** the Result screen — "N / 5", the percentage, the
     "Topic — Difficulty" context line, and one verdict row per question
     (check "Correct" / cross "Incorrect", never color alone).
6. Press **View answers** — the review shows every question with your answer
   and the correct one plus the explanation. Press **Back**.
7. Press **Retake** to run it again, or **Done** to return to the list — the
   status strip announces "Result saved."

Mid-quiz, click another sidebar item (for example History): the confirm
dialog "Leave this quiz? / Your answers so far will be discarded." appears —
**Stay** continues the run, **Leave** discards cleanly (no attempt is saved).

## 3. History (offline-capable)

1. Open **History**. Every attempt is listed: Date, Quiz, Topic, Score
   ("4/5 (80%)").
2. Type in the search box ("Search quizzes…") — the grid narrows live.
3. Use the topic filter and the score filter (Any / 80% and above / 50–79% /
   Below 50%) — try a combination that empties the grid: the filtered-empty
   state appears with a "Clear filters" action.
4. Open an attempt (double-click a row or press its **Review** action).
   - **Expected:** the review — quiz title, the score line, and per question:
     your answer vs the correct answer and the explanation. Press **Back**.

## 4. Reports (offline-capable)

1. Open **Reports**. The summary card shows Attempts, Quizzes taken, and the
   Average score; the per-topic table shows Average %, Best %, and the
   "Needs practice" flag; the weak-topic list ranks lowest averages first.
2. Change the difficulty filter (All / Easy / Medium / Hard) — every number
   recomputes. Try the topic filter.
3. With a topic averaging below 70%, the topic carries the flag in both the
   table and the list.

## 5. Quiz management (offline-capable)

1. Open **Topics**: create a topic (for example "Cloud Security",
   Cybersecurity) — the strip confirms it. Try creating a duplicate name: a
   specific duplicate message appears, nothing crashes.
2. Open **Quiz management**: create a quiz under the new topic; add questions
   with exactly one correct choice each; edit one; reorder with the row
   actions; search by title; filter by status/topic.
3. Try deleting the seeded quiz "C# Core Concepts": blocked with a friendly
   message (it already has attempts — the foreign key protects the history).

## 6. Generate a quiz (needs internet + a provider key)

1. Open **Generate quiz**. Pick a topic, a difficulty, and a question count
   (5–20). The note reads "Needs an internet connection…"

### One-time setup: your free AI key

Quiz generation runs through OpenRouter's **free** models. Every user brings
their own free key — nobody shares one (sharing a key would mean sharing one
account's daily quota, and public keys get revoked):

1. Create a free account at **openrouter.ai** (no credit card needed).
2. Open **Keys** → **Create key**, and copy it (starts with `sk-or-v1-…`).
3. Next to the app, open `config/config.local.json` and paste the key into
   the `openrouter` entry's `"apiKey"` value:

   ```json
   {
     "aiProviders": [
       { "name": "nvidia",     "baseUrl": "https://integrate.api.nvidia.com/v1", "apiKey": "", "model": "meta/llama-3.3-70b-instruct" },
       { "name": "openrouter", "baseUrl": "https://openrouter.ai/api/v1",        "apiKey": "sk-or-v1-YOUR-KEY", "model": "nvidia/nemotron-3-ultra-550b-a55b:free" }
     ]
   }
   ```

4. Save the file. Keys stay on your machine — the app never sends them
   anywhere except the provider's own API, and they are never logged or shown.

**Backup providers (verified free tiers):** if OpenRouter's free quota runs
out (its free tier allows ~50 generated quizzes per day per account), swap in
one of these — both verified free, no credit card, and speak the same
protocol, so it's a config edit only:

| Provider | baseUrl | Free limits | Example model |
|---|---|---|---|
| **Groq** (recommended backup) | `https://api.groq.com/openai/v1` | 30/min, 1,000/day | `openai/gpt-oss-120b` |
| **Google Gemini** | `https://generativelanguage.googleapis.com/v1beta/openai` | free tier via AI Studio | `gemini-3.8-flash` |

Get a Groq key at console.groq.com (no card) and put it in a third
`aiProviders` entry — the app tries providers in order, so a failed tier
falls through to the next one automatically.

The app is deliberately designed so that **no key is baked into the program**:
your key is your account and your quota, and the developer's key is never
exposed inside the download.

### Generating

2. Press **Generate**.
   - **Expected (online, keys configured):** the busy block — a sweeping
     progress bar and a live elapsed-seconds counter (the window stays
     responsive; Cancel returns immediately). On success: "Quiz generated and
     saved as a draft." and a **Review now** button.
   - **Expected (offline or no keys):** the warning banner
     "Quiz generation is unavailable right now." with the plain-language
     reason and the offline pointer — the app stays fully usable. This is a
     *good* demo moment: show it deliberately.
3. Press **Review now** (or open **Review** from the sidebar).

## 7. Review and approve (offline-capable for drafts already saved)

1. The Review list shows the drafts with their Draft badges.
2. Open a draft: each question shows the AI's claimed correct answer
   highlighted ("AI's answer") plus its explanation — highlighted, not
   trusted.
3. Press **Approve**.
   - **Expected:** the badge flips to Approved in place and the strip says
     "Quiz approved."
4. Return to **Take quiz**: the approved quiz is now startable. (A draft is
   never startable anywhere in the app.)
5. Optionally press **Edit questions** in a review to correct the AI, and
   **Discard** on an unwanted draft (confirm first: "The quiz and all its
   questions will be permanently deleted.").

## 8. Close

Close the window (or "Log out" — mid-quiz it asks the leave question first).
Nothing is lost: every completed attempt is already in the local database.
