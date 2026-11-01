# CodeQuest — AI Quiz Provider Wire Contract (NVIDIA NIM + OpenRouter chain)

> Status: `active` — authored Phase P2 (2026-11-02) by the contractor agent.
> History note (the only Gemini reference in this file): the original project proposal named
> Google Gemini as the AI provider; an owner directive dated 2026-11-02 replaced it with
> NVIDIA NIM and OpenRouter ("both" preferred). The never-written `contracts/gemini-quiz-api.md`
> is superseded by this file; no Gemini endpoint appears anywhere below.
> Provenance: governing plan `plans/codequest-master-plan.md` (API Decision Record) and
> `database/schema.sql` (P1). This contract is self-contained: consumers need nothing else.

## 1. Purpose, Scope & Consumers

This contract is the single authoritative description of how CodeQuest talks to external AI
providers to generate multiple-choice quizzes. It fixes:

- the wire format of the two AI providers (NVIDIA NIM, OpenRouter) — both OpenAI
  chat-completions compatible, so **one client, parameterized per provider**, covers both;
- the provider-chain semantics (order, skip rules, typed outcomes);
- the strict-JSON quiz output the app demands from any model, and its exact mapping to
  `database/schema.sql`;
- the six error classes and the typed failure representation the UI displays;
- the shape of `config/config.local.json` (`aiProviders` section);
- the quiz status workflow every generated quiz enters (`Draft` → `Approved`).

**Consumers (phases that code against this file):**

| Phase | Consumes |
|---|---|
| P3 | defines `IQuizProvider` (Services/IQuizProvider.cs) — the seam this contract's chain plugs into |
| P5 | implements `ChatCompletionsClient` + DTOs, `AiQuizProvider` (chain), `OfflineQuizProvider`, `QuizGenerationService`, `AppConfig` from §3–§8; edits `config/config.example.json` to §8 |
| P13 | builds the generation + review/approve UI on the §7 typed outcomes and §9 status workflow |
| P15 | audits key handling per §8 (keys never committed/logged/hard-coded) |

**Out of scope (deliberately):** quiz-taking, scoring, attempts, reports (offline features);
the offline provider's DB queries (it is named here, specified by P5 against P3/P4 interfaces);
UI wording; model choice (slugs are config, never hard-coded — §5.3); provider-side JSON-mode
features (§6 forbids relying on them).

## 2. Conventions & Tier Identifiers

- **Tier identifier** — the value of a config entry's `name` field (§8), used verbatim in
  results and reason strings. The two wire tiers are `"nvidia"` and `"openrouter"`.
  A third identifier, `"offline"`, is reserved for the seeded-DB provider (§3.4); it has no
  config entry and performs no HTTP.
- **Generation outcome (app-side result type, P5 implements):** a discriminated result, never
  a thrown exception crossing to the UI:
  - `Success` — `ServedByTier` = `"nvidia"` | `"openrouter"`; `QuizDraft` = the parsed quiz (§6).
  - `Failure` — `ServedByTier` = null; `ProviderReasons` = array of typed reason strings
    (§7.7), one per provider, in config order.
- **Reason string grammar** (printable ASCII, no newlines, never contains key material):
  `<tier>:<class-code>[:<detail>]` — e.g. `nvidia:quota:429;retry-after=12s`.
- **Field naming:** request/response JSON to providers uses OpenAI `snake_case` wire names;
  the model-output quiz JSON (§6) and all app-side DTOs use `camelCase`.
- **Timestamps:** all `created_at` values are app-generated ISO-8601 UTC
  (e.g. `2026-11-02T14:05:00Z`); the model never supplies timestamps or IDs.

## 3. Provider Chain Semantics

### 3.1 Chain order

The AI chain is the `aiProviders` array from `config/config.local.json` (§8). **Array order is
chain priority**: entry 0 is tried first, then entry 1, and so on. The shipped order is
`nvidia` → `openrouter`; reordering the array re-prioritizes the chain with no code change.

### 3.2 Skip rules (no HTTP attempt is made for a skipped entry)

An entry is skipped — and appears in `ProviderReasons` with a `not-configured` reason (§7.7),
e.g. `openrouter:not-configured:api-key-empty` — when any of these holds:

| Condition | Reason detail |
|---|---|
| `enabled` is `false` | `disabled` |
| `apiKey` is missing or empty after trim | `api-key-empty` |
| `enabled` is `true` but `baseUrl` or `model` is missing/empty | `invalid-config` |

Skipping is silent by design (per the API Decision Record: the app runs with NVIDIA only,
OpenRouter only, both, or neither — no code-path change), but the skip is always visible in the
failure report if the chain ultimately fails.

### 3.3 Attempt rule and terminal result

- Each non-skipped entry is attempted **once**, in order, per §4/§5. No automatic retry within
  a tier (one user action = at most one HTTP request per configured provider).
- On any error class 1–5 (§7), the tier's reason is recorded and the chain moves to the next
  entry.
- The **first tier whose response parses into a valid quiz (§6)** wins:
  `Success { ServedByTier = <its name>, QuizDraft }`.
- If every entry was skipped or failed, the chain returns the terminal typed result
  `Failure { ServedByTier = null, ProviderReasons = [...one per entry, config order] }`.
  **This is a typed failure object, never an exception reaching the UI.** The UI (P13) renders
  it per §7 and points the user at the offline path.

### 3.4 The offline tier — outside this wire contract

`OfflineQuizProvider` (P5) is the last-resort tier behind the same `IQuizProvider` seam. It
reads existing quizzes from the SQLite database — it performs **no HTTP** and has **no entry
in `aiProviders`**. Two consequences this contract fixes:

1. **It never serves generation.** A generation request that exhausts the AI chain ends in the
   typed `Failure` above; the offline tier cannot author new quizzes. (Plan P5 acceptance:
   with no keys configured, `QuizGenerationService` returns a typed failure, not an exception.)
2. **Tier reporting.** Any app path that reports which tier served or failed uses the §2 tier
   identifiers: `"nvidia"`, `"openrouter"` for wire tiers, `"offline"` for the seeded-DB
   provider on retrieval paths (takeable quizzes). Generation results never report `"offline"`.

## 4. Shared Wire Envelope (OpenAI chat-completions)

Both providers accept the same OpenAI chat-completions shape (verified: NVIDIA's model page
declares "Compatible with OpenAI" and ships OpenAI-SDK templates; OpenRouter documents the
OpenAI SDK as a drop-in at their base URL). **One client** (`ChatCompletionsClient`, P5) sends
this envelope to either provider, parameterized by §5 profile + §8 config.

### 4.1 Request

`POST {baseUrl}/chat/completions` — headers: `Content-Type: application/json` (required) and
the provider auth header (§5). Body fields the client sends (all verified within both
providers' documented schemas):

| Field | Type | Value | Why |
|---|---|---|---|
| `model` | string | from config (`aiProviders[n].model`) — never hard-coded | selects the model; slugs churn, config does not |
| `messages` | array | exactly 2 items, roles `system` then `user`, `content` strings | system-first is required by NVIDIA when a system message is present; valid on OpenRouter |
| `temperature` | number | `0.2` | deterministic-enough sampling; inside NVIDIA's range 0–1 (their default) and OpenRouter's 0–2 |
| `max_tokens` | integer | `4096` default; if the configured model documents a lower maximum, use that | must cover 20 questions × ≈200 tokens; never rely on provider defaults (NVIDIA's is 1024 → truncation → error class 4) |
| `stream` | boolean | `false` | the client parses one JSON body; no SSE handling |

**Deliberately NOT sent** (both verified): `response_format` — absent from NVIDIA's
`ChatRequest` schema (which sets `additionalProperties: false`, so unknown fields risk HTTP
422) and model-dependent on OpenRouter; `seed` — absent from NVIDIA's `ChatRequest` schema;
`top_p` — NVIDIA advises against combining it with `temperature`, and it is unnecessary here.
Provider JSON modes are never relied upon (§6).

**System message requirements** (P5 writes the final prose; the contract pins the requirements):
messages[0] (`system`) must state the §6 output schema with the exact field names, forbid
markdown fences and any prose around the JSON, require exactly *N* questions (N = 5–20, from
the UI), exactly 4 choices per question, exactly one `correctIndex` in 0–3, a non-empty
`explanation` per question, and echo of the requested `topic` and `difficulty`. messages[1]
(`user`) carries the generation parameters: topic name, topic category, difficulty, N.

### 4.2 Response

Success precondition: HTTP `200` **and** a JSON body with `choices[0].message.content` being a
non-null, non-empty string **and** `choices[0].finish_reason == "stop"`. Any other outcome maps
to an error class (§7). Both providers' documented response envelope:

| Field | Type | Notes |
|---|---|---|
| `choices` | array | at least one item expected; `choices[0]` is used, others ignored |
| `choices[0].message.content` | string \| null | the model output; §6 parses this and only this |
| `choices[0].finish_reason` | string | NVIDIA documents `stop` \| `length` \| `tool_calls` \| null; OpenRouter normalizes to `tool_calls` \| `stop` \| `length` \| `content_filter` \| `error`. The client accepts only `stop` |
| `usage` | object | `{prompt_tokens, completion_tokens, total_tokens}`; logged for diagnostics, never parsed for quiz data |

`finish_reason` values other than `stop` are rejected as error class 4 (a `length` finish means
truncated JSON; `tool_calls`/`content_filter`/`error` mean no usable quiz text).

### 4.3 DTO mapping

`ChatCompletionsClient` extracts `choices[0].message.content` (string) → §6 parser →
`QuizDraft`; `AiQuizProvider` maps the validated quiz JSON onto the P3 domain model with
`status` = `Draft` (§9). No other response field reaches domain code.

## 5. Provider Profiles

Each profile completes the §4 envelope with its method+path, auth, headers, status codes,
error-body shape, and documented limits. Everything not listed as a delta is exactly §4.

### 5.1 NVIDIA NIM (primary tier, `"nvidia"`)

- **Method + path:** `POST https://integrate.api.nvidia.com/v1/chat/completions`
  (config `baseUrl`: `https://integrate.api.nvidia.com/v1`). Verified on the NIM LLM APIs
  reference (URL `https://integrate.api.nvidia.com`, endpoint `POST /v1/chat/completions`;
  page updated 2026-10-23, fetched 2026-11-02).
- **Auth:** `Authorization: Bearer <apiKey>` — **VERIFIED 2026-11-02**: the model infer page's
  OpenAPI definition declares security scheme `http/bearer` and its curl template sends
  `-H "Authorization: Bearer $NVIDIA_API_KEY"` on this exact endpoint. (This closes the plan's
  carried-forward unverified item; see §10, U-3.)
- **Other headers:** none beyond §4.1.
- **Request / response:** §4 verbatim.
- **Status codes documented by the vendor:**
  - `200` — success (§4.2 precondition).
  - `202` — "result is pending" (`NVCF-REQID` / `NVCF-STATUS` headers; poll flow). The client
    does **not** implement the poll flow: any `202` is error class 1 (`http-error:202`).
  - `422` — validation failed (request rejected before inference).
  - `500` — invocation error.
  - `401`/`403` — auth failure → class 2. `429` — quota/rate → class 3 (body shape not
    documented on the fetched pages; handled generically, §7.3, §10 U-1).
- **Error body shape (documented):** problem-details object —
  `{type, title, status, detail, instance, requestId}` (e.g.
  `type: "urn:nvcf-worker-service:problem-details:unprocessable-entity"`). The client reads
  only the HTTP status for classification; the body is diagnostic.

### 5.2 OpenRouter (fallback tier, `"openrouter"`)

- **Method + path:** `POST https://openrouter.ai/api/v1/chat/completions`
  (config `baseUrl`: `https://openrouter.ai/api/v1`). Verified: OpenRouter API reference
  documents the Chat Completions request/response schemas and a fetch example on this exact
  URL (fetched 2026-11-02). OpenAI-SDK drop-in compatibility verified on the Quickstart page.
- **Auth:** `Authorization: Bearer <apiKey>` — verified (Quickstart + API reference).
- **Optional attribution headers (verified; P5 MAY send static non-secret values):**
  `HTTP-Referer` (app URL) and `X-OpenRouter-Title` (app name; `X-Title` also accepted).
  Optional — omitting them changes nothing functionally.
- **Request / response:** §4 verbatim. `message.content` is typed `string | null`;
  `finish_reason` is OpenRouter-normalized (§4.2).
- **Error body shape (verified):** `{ "error": { "code": <int>, "message": string,
  "metadata": object } }`. Documented variants: `429` with
  `metadata.error_type = "rate_limit_exceeded"`; `402` credit errors with
  `metadata.limit_source`; `403` budget errors with `metadata.limit_source`
  (e.g. `openrouter_guardrail_budget`).
- **Status codes → classes:** `200` success; `401` → class 2; `403` → inspect body:
  `metadata.limit_source` present → class 3 (`quota` — a budget block, not a bad key);
  otherwise class 2 (auth); `429` → class 3; all other non-200 → class 1.
- **Rate-limit headers (verified):** platform-limit `429` responses carry
  `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`; a `Retry-After` header is
  present when every attempted provider returned a retry hint. **Successful responses never
  carry `X-RateLimit-*` headers.**
- **Free-tier limits (verified, Limits page):** model IDs ending `:free` are capped at
  **20 requests/minute** and, for accounts that have purchased **< 10 credits** (all time),
  **50 requests/day** (≥ 10 credits: 1000/day). `GET https://openrouter.ai/api/v1/key` reports
  `free_model_daily_requests {used, limit, remaining}` — optional monitoring, not required by
  P5. These caps are why OpenRouter is the fallback tier, never the primary.
- **Note:** OpenRouter routes across upstream providers itself (retrying others on 5xx/rate
  limits); this is invisible to the client except through error-body `metadata`.

### 5.3 Model slugs are config, never code

The contract pins **no** model slug. The configured value in `aiProviders[n].model` (§8) is
sent verbatim as the `model` field. Catalog-verified **examples** (present in the vendor
catalogs on 2026-11-02; illustrative only, subject to model churn):

- NVIDIA NIM: `meta/llama-3.3-70b-instruct`, `nvidia/llama-3.3-nemotron-super-49b-v1.5`,
  `openai/gpt-oss-120b`, `qwen/qwen3-next-80b-a3b-instruct`.
- OpenRouter `:free` variants: `nvidia/nemotron-3.5-lightning:free` (1,000,000 ctx),
  `google/gemma-4-31b-it:free` (262,144 ctx), `cohere/north-mini-code:free` (256,000 ctx).

P5's live smoke test validates the actually-configured slugs; a slug that stops existing
surfaces as an error-class-1 HTTP failure and the chain behaves per §3.

## 6. Strict-JSON Quiz Output Schema & Parsing

The §4 system prompt demands exactly this JSON from the model. The parser accepts nothing
else — the answer key must be structurally trustworthy before the human review step (§9).

### 6.1 The JSON the app demands

```json
{
  "title": "C# Fundamentals: Classes and Objects",
  "topic": "C#",
  "difficulty": "Medium",
  "questions": [
    {
      "text": "Which keyword declares a field that can be read but not written outside its class?",
      "choices": ["readonly", "const", "static", "sealed"],
      "correctIndex": 0,
      "explanation": "'readonly' allows assignment only at declaration or in the constructor."
    }
  ]
}
```

Field rules (all enforced; deviation ⇒ error class 4):

| Field | Type | Rule |
|---|---|---|
| `title` | string | trimmed non-empty; ≤ 120 chars |
| `topic` | string | trimmed non-empty; **must equal the requested topic name exactly** (after trim) |
| `difficulty` | string | one of `Easy` \| `Medium` \| `Hard`; **must equal the requested difficulty** |
| `questions` | array | length exactly = requested N (5–20); order = display order |
| `questions[].text` | string | trimmed non-empty |
| `questions[].choices` | string[4] | **exactly 4** items; each trimmed non-empty; **pairwise distinct** (schema `UNIQUE (question_id, choice_text)`) |
| `questions[].correctIndex` | integer | 0–3; identifies the single correct choice |
| `questions[].explanation` | string | trimmed **non-empty** (schema `explanation NOT NULL`; it is the answer key's core) |

Unknown extra keys at root or question level are **ignored** (models occasionally add fields
despite instructions; strictness applies to required content). Everything else — wrong type,
missing key, out-of-range value, wrong count, duplicate choice text, topic/difficulty echo
mismatch — is rejected as malformed output (class 4).

### 6.2 Parsing rules (in order)

1. Take `choices[0].message.content` from the §4.2 envelope (non-null, non-empty, else class 4).
2. Trim whitespace. If the content begins with a Markdown code fence (``` or ```json), strip
   the opening fence line and a trailing ``` fence line. (Models wrap JSON despite instructions.)
3. Parse the **entire** remaining string as JSON — no substring salvage, no "response healing".
   Parse failure ⇒ class 4.
4. Apply every rule in §6.1. First violation stops validation; the detail names the field
   (e.g. `openrouter:bad-output:questions[2].correctIndex=5`).
5. Derive storage positions: `questions[i]` → position `i+1`; `choices[j]` → position `j+1`;
   `is_correct` = `1` where `j == correctIndex`, else `0` (exactly one correct choice per
   question — schema obligation R-2 #3, enforced at insert by P4/P8).

No reliance on provider JSON modes: `response_format` is not sent (§4.1 — verified absent
from NVIDIA's request schema, model-dependent on OpenRouter), so the parser above is the only
JSON guarantee.

### 6.3 Mapping to `database/schema.sql` (column-for-column)

| §6 JSON | schema.sql target | Rule |
|---|---|---|
| `topic` (echo, verified) | — | topic resolution uses the **user's topic picker selection** (an existing `topics` row); the echo is a generation-consistency check only |
| (picker selection) | `quizzes.topic_id` | NOT NULL; FK → `topics` ON DELETE RESTRICT |
| `title` | `quizzes.title` | NOT NULL; **UNIQUE per `(topic_id, title)`** — P5 de-duplicates at insert (e.g. `" (2)"` suffix) so the insert never violates the constraint |
| `difficulty` (echo, verified) | `quizzes.difficulty` | CHECK `('Easy','Medium','Hard')`; difficulty is a **quiz-level** column — the `questions` table has no difficulty column, and none is invented |
| — (app sets) | `quizzes.status` | `'Draft'` at insert (§9) |
| — (app sets) | `quizzes.created_at` | ISO-8601 UTC, app-generated; never model-supplied |
| `questions[].text` | `questions.question_text` | NOT NULL |
| `questions[].explanation` | `questions.explanation` | NOT NULL |
| (array order) | `questions.position` | `i+1`; CHECK `>= 1`; UNIQUE per `(quiz_id, position)` |
| — (app sets) | `questions.created_at` | app-generated UTC |
| `questions[].choices[j]` | `choices.choice_text` | NOT NULL; UNIQUE per `(question_id, choice_text)` |
| `correctIndex` | `choices.is_correct` | `1` at `j == correctIndex`, else `0`; CHECK `IN (0,1)` |
| (array order) | `choices.position` | `j+1`; CHECK `>= 1` |

IDs (`quiz_id`, `question_id`, `choice_id`, `topic_id`) are AUTOINCREMENT at insert — never
model-supplied. No §6 field maps to `users`, `quiz_attempts`, or `submitted_answers` (runtime
tables, not generation targets). The quiz + its questions + choices are inserted in one
transaction (P4's transaction helper).

## 7. Error Classes

All six classes, each with its detection rule, the tier it fails, and the next-tier behavior.
Every failure produces a typed reason string (§2, §7.7) — **no exception ever crosses to the
UI**, and no reason string ever contains API-key material.

### 7.1 Class 1 — HTTP error (non-429)

- **Detection:** any non-200 HTTP status that is not class 2 or class 3 — e.g. 400, 404, 422,
  500, 503, and unexpected 2xx codes (NVIDIA's documented `202` pending state, §5.1).
- **Tier behavior:** the tier fails; the chain tries the next entry (§3.3).
- **Reason:** `<tier>:http-error:<status>` e.g. `nvidia:http-error:500`.

### 7.2 Class 2 — Auth failure (401/403)

- **Detection:** HTTP 401 or 403, except the OpenRouter budget-403 case (→ class 3, §5.2).
  NVIDIA 401/403 → always class 2.
- **Tier behavior:** the tier fails permanently for this attempt (the key is not retried);
  the chain tries the next entry. A persistent auth failure is a user-fixable config problem —
  the UI message points at `config/config.local.json`.
- **Reason:** `<tier>:auth-failed:<status>` e.g. `openrouter:auth-failed:401`.

### 7.3 Class 3 — Quota / rate limit (429, plus OpenRouter budget-403)

- **Detection:** HTTP 429 on either provider; on OpenRouter additionally a 403 whose body
  carries `error.metadata.limit_source` (budget/credit block, §5.2).
- **Header handling:** if `Retry-After` is present, its value is recorded in the reason detail;
  the client **does not sleep-and-retry inside the same user action** (a single generation
  click performs at most one request per provider). OpenRouter platform-limit 429s also carry
  `X-RateLimit-Limit/Remaining/Reset` (§5.2) — diagnostic only.
- **Tier behavior:** the tier fails; the chain tries the next entry. This is the case that
  makes the chain worthwhile (NVIDIA quota exhausted → OpenRouter `:free` takes over).
- **Reason:** `<tier>:quota:<status>[;retry-after=<s>]` e.g. `nvidia:quota:429;retry-after=12s`,
  `openrouter:quota:403;limit_source=openrouter_key_limit`.

### 7.4 Class 4 — Malformed / failed-JSON model output

- **Detection:** any of — `finish_reason != "stop"`; `message.content` null or empty; parse
  failure after fence-stripping (§6.2.3); any §6.1 validation violation.
- **Tier behavior:** the tier fails; the chain tries the next entry (a different model may
  comply where the first did not).
- **Reason:** `<tier>:bad-output:<cause>` e.g. `nvidia:bad-output:finish_reason=length`,
  `openrouter:bad-output:questions[2].correctIndex=5`.

### 7.5 Class 5 — Timeout

- **Detection:** no complete HTTP response within the per-attempt timeout.
- **Timeout value policy (app-side, not a vendor fact):** **60 seconds per provider attempt.**
  Rationale: free-tier instruct models generating up to 20 questions (§4.1 `max_tokens 4096`)
  can legitimately take tens of seconds; 60 s bounds the worst case (both tiers time out) at
  ≈ 2 minutes, which P13's busy indicator communicates. The value is a client constant,
  changeable without contract change.
- **Tier behavior:** the tier fails; the chain tries the next entry.
- **Reason:** `<tier>:timeout:60s` e.g. `openrouter:timeout:60s`.

### 7.6 Class 6 — Empty provider list (nothing to try)

- **Detection:** `aiProviders` missing or an empty array, **or** every entry skipped by §3.2
  (disabled / empty key / invalid config). No HTTP request is made.
- **Tier behavior:** this is a chain-level precondition failure, not a per-tier error. Terminal.
- **Reason:** the per-entry skip reasons (§3.2) when entries exist;
  `chain:empty-provider-list` when the array itself is absent/empty.
- **UI:** the typed `Failure` renders with guidance to configure a key or use the seeded
  offline quizzes (§3.4).

### 7.7 Reason string grammar (summary)

`<tier>:<class-code>[:<detail>]` — class codes: `http-error`, `auth-failed`, `quota`,
`bad-output`, `timeout`, `not-configured`; plus the chain-level `chain:empty-provider-list`.
`<tier>` is the config entry's `name` (§2). Details are short, printable-ASCII, key-free.

### 7.8 Invariants

1. Exactly one `Success` or one `Failure` per generation request — never an exception at the
   UI boundary.
2. At most one HTTP request per configured, non-skipped provider per user action.
3. Reason strings and logs contain no key material (re-audited in P15).
4. A skipped provider is always visible in `ProviderReasons` if the chain fails (§3.2).

## 8. Configuration File Shape

- **Local file:** `config/config.local.json` — git-ignored from the first commit
  (`.gitignore:25`); never committed, never logged, keys never hard-coded (P15 re-audits both
  keys and the git history).
- **Committed template:** `config/config.example.json` — same shape, empty key strings.
  P5 edits the template to this shape; this contract only **documents** it.
- This section defines the `aiProviders` section. The file may carry other app settings
  (e.g. the DB path, owned by P4's `AppConfig` reading) — out of scope here.

```json
{
  "aiProviders": [
    {
      "name": "nvidia",
      "baseUrl": "https://integrate.api.nvidia.com/v1",
      "apiKey": "",
      "model": "meta/llama-3.3-70b-instruct",
      "enabled": true
    },
    {
      "name": "openrouter",
      "baseUrl": "https://openrouter.ai/api/v1",
      "apiKey": "",
      "model": "nvidia/nemotron-3.5-lightning:free",
      "enabled": true
    }
  ]
}
```

Field rules:

| Field | Type | Rule |
|---|---|---|
| `aiProviders` | array | **array order is chain priority** (§3.1); may contain 0, 1, or both entries |
| `name` | string | stable tier identifier used in results/reasons (`"nvidia"`, `"openrouter"`; §2) |
| `baseUrl` | string | provider base URL incl. `/v1`; the client appends `/chat/completions` |
| `apiKey` | string | secret; empty string = entry is skipped (§3.2); never logged or committed |
| `model` | string | model slug sent verbatim as the §4.1 `model` field; never hard-coded (§5.3) |
| `enabled` | boolean | `false` skips the entry regardless of key (§3.2) |

The `model` values above are the catalog-verified examples of §5.3 — operators may replace
them; the contract pins none.

## 9. Quiz Status Workflow (Draft → Approved)

- The only two status values are the ones `database/schema.sql` enforces —
  `quizzes.status TEXT NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft', 'Approved'))`.
- **Every AI-generated quiz is inserted with `status = 'Draft'`** — set at insert time by the
  app (§6.3); the model never supplies a status.
- A `Draft` quiz is **never takeable** by a student (P9 lists Draft quizzes disabled with a
  "pending review" tag).
- **Only the review screen (P13) flips `Draft` → `Approved`**, after a human has checked the
  AI's claimed correct answers and explanations. Approval makes the quiz takeable in the P9
  flow. The reviewer may also edit or discard the draft.
- There are **no** `approved_at` / `approved_by` audit columns (schema decision R-2 #4); adding
  them would be a schema-change phase, not an improvisation.
- Hand-created quizzes (P8 editor) follow the same two-value lifecycle; the AI chain is not
  special with respect to status.

## 10. Sources & Verification Register

**search: done** (2026-11-02) — topics: NVIDIA NIM endpoint/auth/schema, OpenRouter
base URL/auth/headers/limits/error shapes, live model catalogs on both platforms.
Sources are vendor-primary (vendor docs and vendor API endpoints). Internal facts
(schema columns, phase obligations) come from `database/schema.sql` and
`plans/codequest-master-plan.md` — no web page can override those.

### 10.1 Verified external facts (source + access date)

| # | Fact used in this contract | Source (accessed 2026-11-02) |
|---|---|---|
| V-1 | NVIDIA NIM: URL `https://integrate.api.nvidia.com`, endpoint `POST /v1/chat/completions` | NIM LLM APIs reference — https://docs.api.nvidia.com/nim/reference/llm-apis (page updated 2026-10-23) |
| V-2 | NVIDIA auth: HTTP `bearer` security scheme; curl template sends `Authorization: Bearer $NVIDIA_API_KEY` on this endpoint; OpenAI-SDK templates with `base_url = https://integrate.api.nvidia.com/v1` | model infer reference — https://docs.api.nvidia.com/nim/reference/meta-llama-3_3-70b-instruct-infer (page updated 2026-08-06) |
| V-3 | NVIDIA `ChatRequest`: `messages` required (system role first when present); `temperature` 0–1 (default 0.2); `max_tokens` 1–4096 (default 1024); `additionalProperties: false` (no `response_format`/`seed` in schema); `stream` default false | V-2 (OpenAPI definition) |
| V-4 | NVIDIA response: `choices[].message.content`, `finish_reason` ∈ {stop, length, tool_calls, null}; `usage` required; documented 202-pending (NVCF-REQID/NVCF-STATUS) and 422/500 problem-details error body | V-2 (OpenAPI definition) |
| V-5 | OpenRouter: base `https://openrouter.ai/api/v1`, `POST /api/v1/chat/completions`, `Authorization: Bearer`, OpenAI-SDK drop-in | Quickstart — https://openrouter.ai/docs/quickstart; API reference — https://openrouter.ai/docs/api-reference/overview |
| V-6 | OpenRouter optional headers `HTTP-Referer`, `X-OpenRouter-Title` (`X-Title` accepted) | V-5 (both pages) |
| V-7 | OpenRouter response: `message.content` string\|null; normalized `finish_reason` (stop/length/tool_calls/content_filter/error); error body `{error:{code,message,metadata}}`; 429 example with `metadata.error_type`; `X-RateLimit-*` only on error responses; `Retry-After` when every attempted provider returned a retry hint | API reference — https://openrouter.ai/docs/api-reference/overview; Limits — https://openrouter.ai/docs/api-reference/limits |
| V-8 | OpenRouter `:free` limits: 20 req/min; 50 req/day (<10 credits purchased all-time), 1000 req/day (≥10); `GET /api/v1/key` exposes `free_model_daily_requests` | Limits — https://openrouter.ai/docs/api-reference/limits |
| V-9 | OpenRouter catalog contains `:free` text→text instruct models, e.g. `nvidia/nemotron-3.5-lightning:free`, `google/gemma-4-31b-it:free`, `cohere/north-mini-code:free` | live catalog — https://openrouter.ai/api/v1/models |
| V-10 | NVIDIA catalog contains instruct models e.g. `meta/llama-3.3-70b-instruct`, `nvidia/llama-3.3-nemotron-super-49b-v1.5`, `openai/gpt-oss-120b`, `qwen/qwen3-next-80b-a3b-instruct` | V-1 (model tables) |

### 10.2 UNVERIFIED register

| # | Item | Status | Handling / closure |
|---|---|---|---|
| U-1 | NVIDIA 429 (quota) response body shape and any numeric rate limits | **Unverified** — not stated on the fetched NVIDIA pages | Contract detects 429 by status code and honors `Retry-After` only if present (§7.3); no numeric NVIDIA limit is claimed anywhere. Safe by construction. P2 Reviewer / P5 Reviewer may close against live NVIDIA docs |
| U-2 | NVIDIA 401/403 response body shape | **Unverified** — not needed: classification is status-code-based (§7.2) | No closure required; body treated as diagnostic |
| U-3 | NVIDIA auth header (carried forward unverified from the plan's evidence pass) | **CLOSED — verified 2026-11-02** (V-2: OpenAPI `http/bearer` scheme + curl template on the vendor's own endpoint page) | P2 Reviewer still re-verifies against live docs (plan-mandated); P5/P13 Reviewers repeat the same searches before PASS |
| U-4 | Long-term persistence of OpenRouter free caps / NVIDIA free access | **Out of contract scope** — deliberately unverified | The chain degrades by config (disable or delete an entry; empty key = skip, §3.2); with no usable provider the app is offline-only with zero code change (§3.4). Fallback if any wire fact proves unresolvable at P5: configure only the working provider, or none — the typed-failure + offline path is the designed degradation |

### 10.3 Register rule

Any future edit that adds an external fact to this contract must add or update a V-item
(source + access date) or a U-item here. A fact with neither is a contract defect.
