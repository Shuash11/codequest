# CodeQuest — iOS-Style UI Design Spec (Design of Record)

Status: `active` — Phase P6 deliverable. Created 2026-11-04 by the Designer agent. This is the design of record for every CodeQuest screen (P7–P13, polished in P16). The Frontend agent implements against this spec; P16 polishes against its unslop bar (§6). No code lives in this document. Amended 2026-11-06 (P6.1): five accessibility & surface amendments answering the P8 ux-review findings — §2.6, §4.4, §4.6, §4.7, §4.9, §4.10, §4.11, §5 preamble, §5.4, §5.5, §7.

Sources of truth: `plans/codequest-master-plan.md` (Phase P6, P7–P13, R-2/R-3/R-5, backlog #1/#5/#11), `docs/codequest-proposal.md` (read-only), `docs/oop-final-exam-guidelines.md` (read-only, UI rubric lines 109–114). Stack constraint: C# .NET 10 WinForms, GDI+ only — flat colors, custom-drawn controls, restyled `DataGridView`; no WPF, no external UI frameworks.

## 1. Feature list — resolved from proposal prose (backlog #11)

**Resolution of backlog #11, stated explicitly.** `docs/codequest-proposal.md` line 26 contains only the placeholder `[TABLE 1]` where a feature table would sit. That placeholder is **not a feature list and was disregarded**; it is a formatting artifact of the source document (the plan's assumption #5 records the same conclusion for lines 26/31/48). The feature list below is resolved from the proposal's prose only: the concept paragraphs at lines 7–9, the offline/accuracy note at line 23, and the feature bullets at lines 50–56. Screen coverage in §5 is derived from this list.

**The features CodeQuest ships, per proposal prose:**

| # | Feature | Proposal evidence (prose) | Screens carrying it |
|---|---|---|---|
| 1 | **AI-generated quizzes** — create a randomized quiz from a chosen topic, difficulty, and question count; generated questions, choices, correct answers, and explanations save to the local database | Line 7 ("choose a difficulty level and the number of questions"), line 9 ("uses an AI API to create a randomized set of quiz questions… saved in a local database"), line 50 ("Create questions based on the selected topic") | §5.11 Generate quiz |
| 2 | **Multiple topics across programming and cybersecurity** — e.g. C#, Python, Java, C++, JavaScript, SQL, networking, web security | Line 7 (topic examples), line 50 ("Practice programming, SQL, networking, and security"); implemented as the two schema categories `Programming` and `Cybersecurity` (R-2 #7) | §5.3 Topics, §5.11 topic picker |
| 3 | **Difficulty selection — Easy, Medium, Hard** | Line 52 ("Choose easy, medium, or hard") | §5.4 quiz form, §5.11 difficulty picker |
| 4 | **Automatic scoring** — answers checked and each result calculated on submit | Line 9 ("the system checks the answers, calculates the score, and saves the result"), line 52 | §5.6 Quiz-taking, §5.7 Result summary |
| 5 | **Quiz history** — review previous attempts and submitted answers | Line 54 ("Review previous attempts and submitted answers") | §5.8 History, §5.9 Attempt review |
| 6 | **Offline access** — previously generated quizzes and saved results open with no internet; quiz-taking, scoring, and local result storage are fully offline (only new AI generation needs a connection) | Lines 9, 23, 54 | §5.6–§5.9 offline by default; §5.11 typed-failure banner points here |
| 7 | **Performance reports** — results overview and topics that need more practice | Line 56 ("See results and topics that need more practice") | §5.10 Reports |
| 8 | **Login** | **Not in the proposal's prose feature list.** Added per backlog #5: the schema stores user accounts but no login feature was listed; the plan resolves this with a minimal single-user login in P7 validating against the seeded `users` row | §5.1 Login |
| 9 | **Answer-key review/approve workflow** | **In the proposal only as the line 23 note** ("The AI-generated answer key should be checked for accuracy before a quiz is made available to students"). Resolved by backlog #1 into the Draft → Approved status workflow (contract P2; UI in P13): AI quizzes are born Draft, a reviewer approves or corrects them, and only Approved quizzes are takeable | §5.12 Quiz review, Draft/Approved badges in §5.4 |

Two consequences the implementers must not miss: (a) login and review/approve are **additions beyond the proposal's bullet list** — both are plan-resolved (backlog #5 and #1) and both must exist for the system to be coherent, so they appear in the navigation as first-class screens; (b) the feature list deliberately maps 1:1 onto the navigation model in §5.2 — one nav entry per feature cluster — which is also how the design scores the rubric's "organized and consistent interface" and "ease of navigation" points (guidelines lines 109–114).

## 2. Visual system

The palette, type scale, spacing, radii, and separators below are the tokens P7 carries into `UITheme.cs`.

### 2.1 Color tokens

> **Fun-UI amendment (2026-11-08, owner direction: "this is not a fun quiz style ui").**
> The LEARNER surfaces (quiz cards, answer tiles, score bars, result screen)
> add a vivid quiz palette on top of the neutral base: `FunPrimary #6C5CE7`,
> answer tiles Red `#D63040` / Blue `#2D6BE4` / Amber `#C77700` / Green
> `#1E8E4E` (white bold text — large-text contrast ≥ 3:1), and difficulty
> colors green/amber/red. The management screens (Topics, Quiz management,
> Review — where the graded CRUD tables live) keep the D3 neutral palette.
> The grey accent stays the system base; the fun colors are learner-surface
> tokens (`FunPrimary`, `TileRed/Blue/Amber/Green`, `LevelEasy/Medium/Hard`,
> `CorrectWash`, `WrongWash`) in `UITheme.cs` — same single-source rule.

> **D3 re-issue (2026-11-08, owner directive "grey and modern theme").** The blue
> accent is replaced by a grey accent, and the four contrast findings from the
> P8 accessibility review are fixed at the token level (F3: SuccessText /
> DangerText now ≥ 4.75:1 on their tints; F7: TextSecondary headers now
> 4.66:1 on Background; F8: badge text 4.75:1; F9: BorderControl now 3.25:1,
> above the 3:1 non-text floor). The tables below carry the re-issued values;
> `UITheme.cs` is updated to match — the single code revision point the token
> discipline buys. The interim all-`TextPrimary` strip rule from P8.1 is
> discarded: the verdict tokens are semantic again and AA-compliant.

The palette is **light neutral surfaces, one grey accent, three semantic colors.** The original values were pinned from the Apple Human Interface Guidelines (fetched 2026-11-04; see §3 for citation); the D3 re-issue keeps the structure and re-pins the values (the grey ramp and the AA-corrected semantic text variants). WinForms has no dynamic system-color machinery; `UITheme.cs` constants are the single source. Dark mode is out of scope: the app ships one light appearance (deliberate scope note — a half-done dark mode would break the consistency rubric line it is meant to earn).

**Surfaces and neutrals** (the D3 grey ramp, light mode):

| Token | Hex | Used for |
|---|---|---|
| `Background` | `#F5F5F7` | Window background, sidebar background — the grouped-background "primary" role |
| `Surface` | `#FFFFFF` | Cards, list containers, input fields — the grouped-background "secondary" role |
| `Fill` | `#E9E9EE` | Selected/hover row fill, pressed chips, quiet control fills |
| `BorderControl` | `#8E8E93` | Text-input and combo borders; unchecked control borders (F9: 3.25:1) |
| `SeparatorHairline` | `#D8D8DC` | 1px row separators (see §2.5) |

**Text** (three opaque desktop tokens; the first two meet AA on Surface and Background):

| Token | Hex | Role |
|---|---|---|
| `TextPrimary` | `#1D1D1F` | Primary content |
| `TextSecondary` | `#6E6E73` | Metadata: dates, sub-labels, quiet column headers (F7: 5.07:1 on Surface) |
| `TextTertiary` | `#98989D` | Placeholder text, disabled text, decorative hints (AA-exempt roles) |
| `TextOnAccent` | `#FFFFFF` | Text/icons on filled accent or filled danger buttons |

**Accent — exactly one, and grey (D3):**

| Token | Hex | Used for |
|---|---|---|
| `Accent` | `#3A3A3C` | Primary action fill, sidebar active state, focused input border, links (white label 9.1:1) |
| `AccentHover` | `#2C2C2E` | Pointer hover on filled accent buttons — no hover on touch iOS, so desktop needs this added |
| `AccentPressed` | `#232326` | Button while held |
| `AccentDisabled` | `#C7C7CC` | Disabled filled buttons; paired with `TextOnAccent` at reduced expectation, see state tables in §4 |
| `AccentTint` | `#E0E0E5` | Selected sidebar item background, selected DataGridView row, focused control borders |

**Semantic colors** (text variants re-pinned to meet AA on their tints and on white — F3/F8):

| Token | Hex (fill) | Hex (text) | Used for |
|---|---|---|---|
| `Success` | `#2E9E48` | `#157A38` | Approved badge fill, correct-answer marks, success feedback text (text 4.75:1 on tint, 5.5:1 on white) |
| `Danger` | `#C62B25` | `#B3241F` | Destructive buttons, error text, incorrect marks (text 5.6:1 on tint, 6.6:1 on white; white label on the fill 5.5:1) |
| `Warning` | `#A97400` | `#8A5A00` | Draft-phase generation banner border, needs-practice emphasis (text 5.4:1 on tint) |

Tints for banner/badge backgrounds are designer-derived mixes on white: `SuccessTint` `#DFF5E7`, `DangerTint` `#FDE8E9`, `WarningTint` `#FFF3E0`.

**Color has a job.** Every token above signals state or action; none is decoration (HIG: "use color consistently… to help communicate information like status or interactivity"). Accent appears only where a thing is the primary action or the current selection — never on two competing buttons at once, never on passive text. State is never color alone: Draft/Approved badges always carry a text label (§4.10), correct/incorrect always carry a mark plus text (§5.9) — HIG inclusive-color guidance.

### 2.2 Typography

One typeface: **Segoe UI** — the Windows system font, standing in for iOS's SF Pro the same way the plan directs (P16 line: "Segoe UI with a clear 3-step size hierarchy"). HIG typography guidance applied: hierarchy through size, weight, and the label-color ramp together; avoid light weights (Regular is the minimum); one family only.

**The 3-step size hierarchy:**

| Style | Font | Size | Used for |
|---|---|---|---|
| `Title` | Segoe UI Semibold | 16pt | Screen titles in the content header; the score number on the result summary |
| `Body` | Segoe UI Regular | 10pt | Everything else by default: list rows, buttons, inputs, question text, grid cells |
| `Caption` | Segoe UI Regular | 9pt | Metadata: dates, sub-labels, badges, hints, table footers |

**Weight variants inside the three sizes** — this is the iOS "headline" pattern (headline = body size, semibold weight), so emphasis never invents a fourth size:

- `BodySemibold` (Segoe UI Semibold 10pt): row and card titles, the primary button's label, the quiz question being answered, form field labels.
- `CaptionSemibold` (Segoe UI Semibold 9pt): badge text where extra presence is needed.

Mapping note for implementers: the iOS Dynamic Type "Large (default)" scale (Large Title 34 → Caption 2 11) is phone-scale; the desktop scale above preserves its shape — title ≈ 1.6× body, caption ≈ 0.9× body, emphasis by weight at constant size — rather than transplanting point values. Desktop minimum size is Caption 9pt; nothing in the UI is smaller.

### 2.3 Spacing scale (8px grid)

Every gap and margin in the app is one of six steps: **4, 8, 12, 16, 24, 32** (px). No other value appears in any layout.

| Step | Used for |
|---|---|
| 4 | Tight internal pairs only: icon ↔ label inside a control, badge text padding |
| 8 | Control ↔ control inside a group; button padding horizontal |
| 12 | List-row vertical padding; input vertical padding |
| 16 | Screen margin (content inset from shell edges — the iOS 16pt page-margin convention); card ↔ card; field ↔ field in a form |
| 24 | Section gap: between a filter bar and its grid, between grouped cards |
| 32 | Content-header block ↔ content below it |

When in doubt between two steps, take the larger one — generous whitespace is the cheapest way a layout reads as designed rather than generated (and the spacing rule of the anti-slop sources in §3: "AI under-spaces everything").

### 2.4 Corner radius conventions

Radius scales with element size, in three fixed values — one per element class, never one radius on everything:

| Radius | Element class |
|---|---|
| 12px | Large containers: cards, grouped list containers, dialogs |
| 8px | Controls: buttons, text inputs, combo boxes, progress track |
| 4px | Small chips: status badges |

Drawn with `GraphicsPath` arcs (GDI+ has no native rounded rectangle fill); all corners of one element use the same radius; no element mixes radii. This follows the iOS inset-grouped-list silhouette (≈10pt continuous corners) adapted to desktop pixel density.

### 2.5 Hairline separator style

- Row separators inside a list/grid: **1px solid `SeparatorHairline` (#E5E5EA)**, nothing heavier, never a bevel or 3D edge (GDI+ flat drawing only).
- Separators are **left-inset 16px to align with the row's text** and run to the container's right edge — the iOS inset-grouped-list separator convention.
- Between separate cards or groups there is **no separator at all** — a 16px (or 24px, per §2.3) gap does the grouping; the HIG explicitly lists negative space, container shapes, and separator lines as the three grouping tools and this design assigns each its own job (containers group controls, gaps group sections, separators only separate rows within one container).

### 2.6 Accessibility floors

- **Contrast:** body copy is always `TextPrimary` on `Surface`/`Background` (21:1). `TextSecondary` (#8E8E93, ≈3.3:1 on white) is reserved for non-essential metadata at Caption size; anything a user must read to act — validation errors, banner text, empty-state explanations — uses `TextPrimary` or the semantic *text* tokens (which are the HIG increased-contrast variants, chosen for exactly this). White text appears only on `Accent`/`AccentHover`/`AccentPressed`/`Danger` fills (≥3.5:1; large/bold button labels), never on `Fill` or tints.
- **INTERIM — status-strip verdict text (F3, until the R-D3 theme re-issue):** verdict lines in the content-header status strip (§5 preamble) — success AND failure/blocked lines alike — render in `Body` `TextPrimary` on `Background` (AA-compliant with a wide margin). The semantic text tokens currently measure 4.07:1 (`SuccessText`) and 4.08:1 (`DangerText`) on `Background` — below the 4.5:1 AA floor (P8 ux-review) — so they are out of the strip until R-D3 re-issues them with compliant ratios; at that point this mapping is discarded and the strip uses the re-issued tokens. The same tokens keep their on-`Surface` uses (§5.7/§5.9 marks, §4.4 error lines, §4.1 destructive text), where they measure ≥4.5:1 on white.
- **Focus is visible:** every control shows keyboard focus — inputs get a 1px `Accent` border swap plus the caret; buttons and nav items get a 1px `Accent` outline inset 1px; DataGridView rows show the `AccentTint` selection fill. Focus order is top-to-bottom, left-to-right; Tab and arrow keys are first-class navigation (rubric: "forms are easy to navigate").
- **No color-only meaning:** Draft/Approved, correct/incorrect, and the needs-practice flag all pair color with a text label or a distinct mark shape (HIG inclusive-color rule).
- **Target sizes:** interactive rows ≥ 36px tall; buttons ≥ 32px tall; the nav list uses full-width hit areas.

## 3. ui-reference derivations

**Search record (web-research decision):** search done 2026-11-04 — topics: iOS design-system references (color, typography, layout, lists, sidebars, alerts) and current AI-design tells. Primary sources fetched directly from Apple's developer documentation (the canonical iOS design reference, current editions with visible change logs); a second search sweep covered current AI-slop tells. No acceptable-reference gap: five references adopted below.

### 3.1 Apple Human Interface Guidelines — Color

- **Citation:** "Color," Apple Human Interface Guidelines, developer.apple.com/design/human-interface-guidelines/color (system-color values updated June 9, 2025; page © 2026 Apple Inc.). Accessed 2026-11-04.
- **Extracted:** the **grouped background system** (primary = overall view, secondary = grouping surfaces, tertiary = nested grouping) → became `Background`/`Surface`/`Fill`; the **label hierarchy** (label → secondary → tertiary → placeholder, plus separator) → became the three text tokens; the **iOS gray ramp** (systemGray…systemGray6 light-mode values) → §2.1 neutral values verbatim; **system color values** for Blue/Green/Red/Orange including the *increased-contrast* variants → accent and semantic tokens (fills use base values, text uses accessible variants); the semantic rules "avoid using the same color to mean different things," "avoid relying solely on color," and "color communicates status and interactivity" → §2.1 color-has-a-job and §2.6 no-color-only-meaning floors.

### 3.2 Apple Human Interface Guidelines — Typography

- **Citation:** "Typography," Apple Human Interface Guidelines, developer.apple.com/design/human-interface-guidelines/typography (page © 2026 Apple Inc.). Accessed 2026-11-04.
- **Extracted:** the **text-style hierarchy** (iOS Large/default: Large Title 34 → Body 17 → Caption 2 11, with Headline *Semibold 17* — emphasis at body size by weight) → §2.2's three-size scale plus in-size weight variants; "avoid light font weights" (Regular is the floor); "minimize the number of typefaces"; hierarchy via "font weight, size, and color… to emphasize important information." The system-font strategy (SF Pro on iOS) maps to Segoe UI on Windows — the platform system font, so the UI reads native rather than imported.

### 3.3 Apple Human Interface Guidelines — Layout

- **Citation:** "Layout," Apple Human Interface Guidelines, developer.apple.com/design/human-interface-guidelines/layout (updated September 9, 2026; page © 2026 Apple Inc.). Accessed 2026-11-04.
- **Extracted:** "order content by relative importance" (top, leading side) → every §5 screen leads with its title and primary action; "align elements… use indentation to convey hierarchy"; "group related items" using negative space, container shapes, or separator lines → the three-way grouping assignment in §2.5; "progressive disclosure" → review/detail screens are drill-ins from lists, not everything-on-one-form; "differentiate controls from content" → controls sit on `Surface`, content on `Background`; the macOS note "avoid placing controls or critical information at the bottom of a window" → no action bars pinned to the window bottom; primary actions live in the content header row.

### 3.4 Apple Human Interface Guidelines — Alerts

- **Citation:** "Alerts," Apple Human Interface Guidelines, developer.apple.com/design/human-interface-guidelines/alerts (button guidance updated February 2, 2024; page © 2026 Apple Inc.). Accessed 2026-11-04.
- **Extracted:** the alert anatomy (title, optional informative text, up to three buttons) → §4.11's dialog convention; "be direct, and use a neutral, approachable tone"; "write a title that clearly and succinctly describes the situation" (never "Error" alone); **specific button verbs** ("Delete," "Erase," "Clear") instead of a generic OK when an action is confirmed, with "OK" reserved for purely informational dialogs; **always title the cancel button "Cancel"** and never make Cancel the default; the destructive style paired with a Cancel escape; "avoid displaying an alert merely to provide information" and "avoid showing an alert when your app starts" → success feedback is an inline status line, not a dialog (§4.11), and the missing-database startup problem uses a dialog only because the app cannot proceed without one.

### 3.5 Apple Human Interface Guidelines — Sidebars

- **Citation:** "Sidebars," Apple Human Interface Guidelines, developer.apple.com/design/human-interface-guidelines/sidebars (updated June 8, 2026; page © 2026 Apple Inc.). Accessed 2026-11-04.
- **Extracted:** the sidebar on the leading edge navigating "top-level collections of content" → the P7 shell; "show no more than two levels of hierarchy" and "succinct, descriptive labels" → one nav entry per feature cluster (§5.2), plain single-word/short labels; icons in the accent color serving "a clear purpose"; "avoid putting critical information or actions at the bottom of a sidebar" → the account/logout row sits directly beneath the nav list, not pinned to the window's bottom edge.

### 3.6 AI-design-tell sweep (corroborating sources)

- **Citation:** "The anti-slop design system: 20 AI tells and how to fix them," Freeform Systems, freeformsystems.ai/blog/anti-slop-design-system, dated 2026-04-28. Accessed 2026-11-04 via search; corroborated by multiple independent community skill repositories surfaced in the same search (e.g. github.com/mervinj3546/anti-slop-design; github.com/nyrthoughts/anti-slop-design `anti-slop-ui` skill), which converge on the same tells: purple-to-blue gradients, one-radius-on-everything, icon-card rows, floating pill badges, generic typefaces.
- **Status per the web-research source filter:** secondary/community sources — used only to confirm that the tell list in §6 is current, never as design authority. The binding checklist is the project's own unslop sign list (plan P16 five-line direction), restated item-by-item in §6.
- **Extracted and applied:** solid colors, never gradient backgrounds; exactly one accent plus a gray ramp; one radius value per element class (§2.4); borders instead of drop shadows on cards; no decorative orbs/aurora; and the spacing rule "AI under-spaces everything — spacing creates grouping, and grouping creates meaning" → §2.3's step-up-when-in-doubt rule.

### 3.7 Deliberately not taken from the references

- **Liquid Glass / translucency materials** (iOS 26 aesthetic, prominent in the 2025–26 HIG color/sidebar pages): unreproducible in GDI+, and gloss/blur reads as slop on a desktop form. The flat grouped-list language underneath it is what this spec takes.
- **Dynamic Type scaling:** no WinForms equivalent; a fixed three-step scale is specified instead (§2.2).
- **SF Symbols iconography:** replaced by text labels, with the Windows system glyph font (Segoe MDL2 Assets, shipping with Windows 10/11) permitted for the few icons that earn their place — sidebar items and the correct/incorrect marks — at weights matched to the adjacent text. No external icon assets, no emoji.
- **Action sheets / bottom sheets:** iOS touch idioms with no desktop meaning; confirmation flows use the §4.11 dialog instead.

## 4. Control restyle recipes (WinForms / GDI+)

Every recipe assumes: `FlatStyle.Flat`, all chrome owner-drawn with GDI+ (`GraphicsPath` for radii, flat `Fill` — no gradients, no drop shadows, no bevels anywhere), tokens from §2, sizes on the §2.3 grid. Height numbers are for standard-DPI; implementers scale with `DeviceDpi`.

### 4.1 Flat button — three variants

All: 8px radius, 32px height, 16px horizontal padding, `Body` label, no border unless stated.

**Primary (filled)** — one per screen region, the main action:

| State | Recipe |
|---|---|
| Default | `Accent` fill, `TextOnAccent` label |
| Hover | `AccentHover` fill (pointer feedback — an addition over touch iOS, which has no hover) |
| Pressed | `AccentPressed` fill |
| Focus (keyboard) | Default paint + 1px `Accent` outline inset 1px |
| Disabled | `AccentDisabled` fill, `TextOnAccent` label; disabled by `Enabled=false` (e.g. Generate while a generation runs) |

**Secondary (plain)** — cancel/auxiliary actions: transparent fill, `Accent` label, no border. Hover: `Fill` background. Pressed: `FillPressed` (`#D8D8DE`, designer-derived darker step of `Fill`). Focus/disabled per the table above, disabled label `TextTertiary`.

**Destructive** — delete/confirm-destroy: `Danger` fill with `TextOnAccent` label; hover `#E62E32`, pressed `#CC1F2A` (designer-derived darken steps); row-level destructive text actions use `DangerText` label on transparent instead of a fill. Disabled: `#F5B5B7` fill (derived tint).

### 4.2 Rounded panel / card

A `Panel` subclass: `Surface` fill, 12px radius, **no border and no shadow by default** — the white-on-`Background` contrast does the separation (§3.6: borders instead of shadows). A 1px `SeparatorHairline` edge is permitted only when a card must sit on a white surface (e.g. inside a dialog card). Groups of controls inside one card get 8px internal gaps and a 16px inset from the card edge.

### 4.3 Sidebar nav item

Full-width row, 40px tall, 16px horizontal padding, glyph (Segoe MDL2 Assets, 12pt) + `Body` label with an 8px gap. States:

| State | Recipe |
|---|---|
| Inactive | transparent fill (on `Background`), `TextPrimary` label, `TextSecondary` glyph |
| Hover | `Fill` background |
| Active | `AccentTint` background, `Accent` glyph, `BodySemibold` label |
| Pressed | `#D6EBFF` background (derived darker tint step) |
| Focus | 1px `Accent` outline inset 1px, in addition to any state |

Hit area is the full row width. One item is active at a time; the active item always matches the visible panel (rubric: ease of navigation).

### 4.4 Text input

`TextBox` (single- and multi-line): `Surface` fill, 8px radius, 1px `BorderControl` border, 32px height (single-line), 12px vertical padding, `Body` text, native `PlaceholderText` in `TextTertiary` ("Search topics…", "Quiz title"). States:

| State | Recipe |
|---|---|
| Default | `BorderControl` border |
| Focus | border color swaps to `Accent` (1px; no glow) + caret |
| Error | border `DangerText` + an inline error line directly below in `DangerText` `Caption` (8px gap), e.g. "Enter a topic name." — the error line persists until the value validates; it never appears as a dialog |
| Disabled | `Fill` background, `TextTertiary` text, no border |

Multi-line editors (question text, explanation): same recipe, minimum 64px height, top-aligned.

**Accessible naming (F2, WCAG 4.1.2):** a field's accessible name equals its visible label text verbatim. Visible field labels (§2.2 `BodySemibold`) are programmatically associated with their inputs — the label is the source of the input's accessible name, not a string retyped beside it. Fields without a visible field label (the toolbar search boxes) take their name from their visible placeholder text. This is implemented with the same `AccessibleObject`/`CreateAccessibleInstance` mechanism the custom controls use (§4.7/§4.9/§4.10); placeholder text is a hint and never a substitute for a label where a label exists.

### 4.5 ComboBox

For bounded-choice fields — the category dropdown, filter combos. `DropDownStyle = DropDownList` **always** (free text is impossible by construction — the R-2 #7 lock for category). Recipe: `Surface` fill, 8px radius, 1px `BorderControl` border, 32px height, `Body` text; the dropdown list renders `Surface` rows with hairline separators, `Fill` hover row, `AccentTint` selected row; the drop arrow is a small chevron glyph in `TextSecondary` (no native 3D arrow). Focus: border swaps to `Accent`. Disabled: `Fill` background, `TextTertiary`. The category dropdown contains exactly two items: `Programming`, `Cybersecurity` — never an editable field, never a third value.

### 4.6 DataGridView as an iOS list

The management, history, and report grids all read as iOS inset-grouped lists, not as a data grid:

- **Container:** the grid sits inside a §4.2 card (12px radius, `Surface`), its own `BorderStyle = None`.
- **Row headers:** invisible (`RowHeadersVisible = false`).
- **Column headers:** quiet — 32px tall, `Background` fill (`EnableHeadersVisualStyles = false`), `Caption`-size `TextSecondary` labels, no 3D chrome, sorted-column indicator a small plain glyph, single 1px `SeparatorHairline` under the header row.
- **Row separators:** `CellBorderStyle = None`; each row paints its own 1px `SeparatorHairline` line (row `RowPostPaint`), **left-inset 16px** to align with row text, running to the right edge (§2.5).
- **Rows:** 36px tall, 12px vertical padding, `Body` text in `TextPrimary`; alternating row colors **off**.
- **Selection:** `FullRowSelect` with `SelectionBackColor = AccentTint`, `SelectionForeColor = TextPrimary` — **never** the default blue selection chrome.
- **Disabled rows** (Draft quizzes in the take list): `TextTertiary` text, not selectable, with the Draft badge (§4.10) inline.
- **Scrollbars:** left as native Windows scrollbars — restyling them in GDI+ is high-risk for zero consistency gain; accepted as platform chrome (recorded decision).
- **Empty grid:** zero rows + the screen's empty-state panel (§5); the grid never renders its own message.
- **Keyboard-operability of row actions (F1, WCAG 2.1.1):** the trailing action column keeps its painted recipe — quiet text actions per §4.1's secondary/destructive text styles (§5.3, §5.4, §5.5) — and gains this interaction contract, which every §4.6 grid with an action column honors identically:
  - **Reach:** the focused cell moves with the arrow keys (left/right/up/down) and with Tab, cell by cell through the whole grid — including into the trailing action column, in both directions (Shift+Tab reverses). §2.6's rule (Tab and arrows are first-class navigation) applies inside the grid, not just between controls.
  - **Activate:** `Enter` or `Space` on a focused action cell invokes that row's action (Space is the button idiom, Enter the list idiom; both work so no key memory is needed). The grid's default Enter behavior (move down one row) is suppressed only while an action cell has focus. Disabled action cells (§5.5's up on the first row, down on the last) render their glyph in `TextTertiary` and activation is a no-op. Where a screen defines a row-level activation (§5.8 opens the attempt review), `Enter` on its rows performs it; action cells always activate their own action.
  - **Focus indicator:** the focused action cell paints the §4.1 focus ring — a 1px `Accent` outline inset 1px — over its normal paint, while its row keeps the `AccentTint` selection fill. No glow, no halo — the same ring vocabulary as buttons, nav items, and segments.
  - **Discoverability:** a focused row's accessible description lists its actions in column order (e.g. "Actions: Edit, Edit questions, Delete."), so a screen-reader user hears what the row offers when the row takes focus; each action cell's accessible Name is its visible text (the §4.4 naming rule — "Edit", not "Edit button for row 3"). Screens with an action column also show one quiet hint line directly beneath the grid card — `Caption` in `TextSecondary`, 8px below the card: "Enter activates a focused row action." — so keyboard activation is documented on-screen, not discovered by accident.

### 4.7 CheckBox

Custom-drawn 16px rounded-4px square: unchecked = `Surface` fill + 1px `BorderControl`; checked = `Accent` fill + white check glyph; focus = 1px `Accent` outline; disabled = `Fill` fill + `TextTertiary`. Label `Body` in `TextPrimary`, 8px gap. Used for per-choice correct-answer marking in the question editor (a question has exactly one correct choice — the editor enforces it with the §5.5 message, the checkbox just marks it).

**Accessibility contract (F2, WCAG 4.1.2):** the control is never absent from the UIA tree and never an unnamed pane. It overrides `CreateAccessibleInstance` to expose its own `AccessibleObject`: **role** `CheckBox`; **name** = its visible label's text verbatim — the `Label` this recipe pairs with the box is the source of the name (§4.4's association rule), never a longer invented description; **state** = checked/unchecked mapped to the UIA toggle state, so marking and unmarking a choice is announced as it happens. §5.5 fixes the per-row visible wording ("Mark choice n as the correct answer"), so the name follows it exactly. Real `CheckBox`/`RadioButton` hosts are NOT used — the overrides carry the accessibility; the custom visuals stay untouched.

### 4.8 RadioButton

Custom-drawn 16px circle: 1px `BorderControl` ring on `Surface`; selected adds an 8px `Accent` dot; focus/disabled as above. Only where a visible one-of-N grouping is clearer than a combo (e.g. correct-choice selection alternative to §4.7 — pick one mechanism per form, not both).

### 4.9 Segmented control

For 2–4 fixed, equal options (difficulty Easy/Medium/Hard; status filter All/Draft/Approved): a single 8px-radius container filled `Fill`, containing equal-width segments; each segment 32px tall, `Body` label; **selected segment = `Surface` fill + `BodySemibold` label** (the iOS segmented style), unselected = transparent + `TextPrimary`. Hover on unselected: `#ECECF1` (derived quiet step). Focus (keyboard): the group is one Tab stop and arrow keys move the selection within it (§2.6); the focused segment carries the standard 1px `Accent` outline inset 1px — the §4.1/§4.3 ring — over its selected or unselected paint. Disabled (whole control, e.g. the §5.11 generation lock): segments stop responding to hover and clicks, every label renders `TextTertiary` (the §4.4/§4.5 disabled tokens), and the selected segment keeps its `Surface` fill so the stored value stays legible. No per-segment borders. This is the difficulty picker and the quiz-status filter — never free text, never a different pattern per screen.

**Accessibility contract (F2, WCAG 4.1.2):** the group overrides `CreateAccessibleInstance`: **role** `Group` with **name** = the group's visible label ("Difficulty", "Type", "Status" — every segmented control sits behind a visible label; the §5.4 toolbar filter carries one). Each segment is a child accessible object: **role** `RadioButton` (one-of-N selection semantics), **name** = the segment's visible text, **state** = `Selected` on the active segment. Keyboard selection is already this recipe's one-Tab-stop/arrow contract; the exposed selected state makes the current value perceivable without moving focus.

### 4.10 Status badges (Draft / Approved)

20px-tall chips, 4px radius, 8px horizontal padding, `CaptionSemibold` text — a badge is always a **text label plus its color** (never color alone):

| Badge | Recipe |
|---|---|
| Draft | `Fill` background, `TextPrimary` text |
| Approved | `SuccessTint` background, `SuccessText` text |

They appear in the quiz management grid (status column), on the quiz review screen, and anywhere a quiz's state matters (§5.4, §5.12). The take-quiz list shows the Draft badge on disabled rows as the "pending review" tag (P9).

**Accessibility contract (F2, WCAG 4.1.2):** a badge exposes **role** `StaticText` and **name** = its text ("Draft" / "Approved") — this recipe already requires the text on every badge, so the name equals the visible label by construction. Wherever the badge renders — as a control or painted inside a §4.6 cell — the UIA element carries this role and name, never a bare unnamed pane. A badge is non-interactive: no focusable or toggle state is exposed.

### 4.11 MessageBox / dialog conventions

`System.Windows.Forms.MessageBox` is **not used** — it cannot be styled. All confirmations and blockers run through one custom modal dialog form:

- **Layout:** a `Surface` card (12px radius) centered over the owner window; no drop shadow, no dimmed backdrop (flat modal — the owner simply stops accepting input). Min width 400px. Content inset 24px. Title in `BodySemibold`, message in `Body` `TextPrimary`, buttons in a right-aligned (trailing) row, 8px apart.
- **Buttons (HIG alert rules, §3.4):** the action the user is confirming is a specific verb on a filled button ("Delete", "Approve", "Discard"), default button on the trailing side; every dialog that performs or precedes a destructive/irreversible action includes a "Cancel" secondary button that is never the default; purely informational dialogs may use "OK"; "Yes"/"No" are never used.
- **Copy:** title = the situation in one line ("Delete this topic?"); body = one short sentence stating the consequence ("This topic will be removed. This cannot be undone." — §5.3's confirm body, offered only when the topic has no quizzes; §5.3 defines both delete branches). No emoji, no exclamation marks, no "Error" as a title, no error codes as titles.
- **Success feedback is not a dialog.** After any create/update/delete, the screen shows an inline success line — `Body` text in the content-header status strip (§5.2), auto-clearing after ~4 seconds ("Topic saved.", "Quiz approved."). HIG: don't interrupt with an alert to convey information. **INTERIM strip token (F3, until the R-D3 theme re-issue):** all verdict lines in the strip — success AND failure/blocked — render in `TextPrimary` on `Background` (§2.6's interim rule; the semantic text tokens measure 4.07:1/4.08:1 there, below AA). Nothing else distinguishes verdict lines in the interim — the wording carries the meaning — and R-D3's re-issued tokens replace this mapping.
- **Validation errors** are inline (§4.4/§4.5), never dialogs. **Unexpected exceptions** (P14): one dialog — title "Something went wrong", body a plain-language sentence plus a hint to retry, buttons "Try again" (secondary) and "Close"; never a stack trace, never raw exception text.
- **Startup blocker (P7):** if the database file is missing at launch — title "CodeQuest can't find its database"; body "The database file was not found next to the app. Restore codequest.db or run database/schema.sql and database/seed.sql, then start the app again."; single "Close" button. This is the one startup dialog; nothing else interrupts launch.
- **Modals never navigate (F5).** The dialog card serves confirmations, blockers, and the small create/edit forms (§5.3, §5.4). Screens that navigate into content — the question editor (§5.5), attempt review (§5.9), quiz review (§5.12's review view) — are shell drill-in panels (§5 preamble), never a second window: no default window chrome, no duplicated title; the drill-in's in-content header is the only title.

### 4.12 Numeric stepper (question count)

`NumericUpDown` restyled: `Surface` fill, 8px radius, 1px `BorderControl` border, 32px height, value `Body` in `TextPrimary`; the two spin zones render as quiet `−`/`+` glyph buttons (`TextSecondary`, hover `Fill`) inside the right edge. Min 5, max 20 (P13's range); typed out-of-range input clamps on leave with the inline hint "Choose between 5 and 20 questions." Focus (keyboard): the border swaps to `Accent` exactly per §4.4 (1px, no glow) plus the caret; the field is one Tab stop and Up/Down arrows step the value. Disabled (e.g. the §5.11 generation lock): `Fill` background, `TextTertiary` value text, no border — §4.4's disabled recipe — and the `−`/`+` zones render `TextTertiary` with no hover response. A plain `ComboBox` prefilled 5–20 is an acceptable fallback if the custom stepper proves fragile — but one of the two, never a bare default `NumericUpDown`.

### 4.13 ProgressBar / busy indicator

For the P13 generation call (and any other long operation):

- **Track:** full-width of the form column, 8px tall, `Fill` fill, 4px radius (pill).
- **Indeterminate bar:** `Accent` segment sweeping via marquee (`ProgressBarStyle.Marquee`) — generation duration is unknowable in advance (§5.11), so the visual promise is activity, not percent. **No percent, no fake progress, no estimated bar** — a fake number is a lie and a looping-animation slop sign.
- **Status line:** directly below the track, `Caption` in `TextSecondary`: "Generating quiz…" plus a live elapsed-seconds counter (updates via the UI timer while the call runs off-thread).
- **Controls during the run:** the Generate button enters its disabled state; a secondary **Cancel** button appears beside the track. The UI stays responsive — the generation call runs off the UI thread (R-5 #3 / R-3 threading note; binding — a frozen window is a review defect), and this indicator is the visible proof.
- **No looping decorative animation exists anywhere else in the app** — the marquee is the single animated element, it only runs while real work runs, and it stops the moment the call returns (§6, stillness rule).

## 5. Screen-by-screen layouts (P7–P13)

Every screen follows the same anatomy: content header row (screen `Title` at the leading side, any primary action trailing), a 20px status strip beneath it for success/failure feedback (§4.11 — dialogs are never used for success; verdict lines use the §2.6/§4.11 interim token until R-D3), then the screen's content on `Background` with 24px inset. All screens share one window; modules are panels the shell swaps (P7), so the sidebar and header never move — the rubric's "consistent and organized interface." Drill-in screens (§5.5, §5.9, §5.12's review view) are panel states the shell swaps inside their module — never separate windows (F5): no default window chrome, no duplicated title; the drill-in's in-content header (with its leading "Back" affordance, §5.9/§5.12's pattern) is the only title, and the sidebar and header stay unchanged while it is open — the module's nav item remains active, nothing dims, nothing disables.

### 5.1 Login (P7 — `LoginForm`)

- **Layout:** centered fixed window (≈420×460) on `Background`. Vertically: app name "CodeQuest" (`Title`, `TextPrimary`), subtitle "Programming and cybersecurity practice quizzes" (`Caption`, `TextSecondary`), 24px, then one card (§4.2) holding the form: Username field, Password field (masked), 16px, "Sign in" primary button (full card width). Inline error line slots under the password field (§4.4).
- **Controls:** two `TextBox`es (§4.4, placeholders "Username" / "Password"), primary button (§4.1), card panel, inline error label. Enter submits; Tab order username → password → button.
- **States:** default (placeholders visible); **validating** ("Signing in…" caption, button disabled); **error** — wrong credentials show the inline line "Incorrect username or password." in `DangerText`, non-blocking, the user stays on login and the password field clears (P7 acceptance); **startup blocker** — if the database file is missing, the §4.11 startup dialog appears before this form renders anything else.
- **Feedback:** success opens the shell — no dialog, no animation. Failure is always the inline line above.

### 5.2 Main shell + navigation (P7 — `MainForm`)

- **Layout:** minimum window 1024×640, `Background` fill. **Sidebar** (leading side, 220px, `Background` with a 1px `SeparatorHairline` right edge): 24px inset; "CodeQuest" (`BodySemibold`); 24px; the nav list; 16px; the account block (signed-in name, `Caption` `TextSecondary`; "Log out" as a secondary-style nav item). **Content** (trailing side): header row, status strip, then the active module panel inset 24px.
- **Nav entries** (§4.3 items — one per feature cluster from §1, ≤ two levels, succinct labels): **Take quiz** (P9, home), **History** (P10), **Reports** (P12), **Generate quiz** (P13), **Review** (P13), **Topics** (P8), **Quiz management** (P8).
- **Controls:** nav item list (custom), content header label, status strip label, module panels.
- **States:** "Take quiz" is active after login. Panels persist (show/hide, not recreate) so switching modules never loses in-progress data — P7's "returns without data loss" is a structural rule, not a save feature. While a generation is running (§5.11) the other nav items are disabled with the caption "Generation in progress."
- **Feedback:** the shell itself is silent; all feedback lives in the module's status strip. Log out returns to the login form; if the user is mid-quiz, the §5.6 leave-confirmation applies first.

### 5.3 Topic management (P8 — `TopicManageForm`)

- **Layout:** header "Topics"; toolbar row (8px gaps): search box (§4.4, "Search topics…", 240px), "New topic" primary button trailing; 16px; one full-height grid card (§4.6): columns **Name** · **Category** · **Quizzes** (count) · **Created**. Row actions (Edit, Delete) as quiet secondary text buttons in a trailing action column.
- **Controls:** search `TextBox`, primary button, `DataGridView` (§4.6), topic modal form, delete dialog (§4.11).
- **Topic form (create/edit):** modal card titled "New topic" / "Edit topic": **Name** (§4.4 input), **Category** (§4.5 ComboBox with exactly two items — `Programming`, `Cybersecurity`; `DropDownStyle = DropDownList`; never free text — R-2 #7; adding a category is a schema-change phase, not a UI affordance). Buttons: Save (primary) / Cancel (secondary).
- **Search:** live filter while typing (P8: "search filters the grid live"); no submit button.
- **States:** **empty** — grid shows nothing and a centered panel reads "No topics yet" (`Body`, `TextPrimary`) + "Choose New topic to add the first one." (`Caption`, `TextSecondary`); **no search match** — "No topics match your search." + "Clear search" secondary; **loading** — none (local SQLite reads are instant; no spinner is designed); **read error** — §4.11 unexpected dialog.
- **Feedback:** success — "Topic saved." / "Topic updated." / "Topic deleted." in the status strip. Failure — inline form errors: "Enter a topic name." (empty), "A topic with this name already exists." (duplicate — the R-2 #5 friendly-duplicate rule). Delete flow — schema fact: `quizzes.topic_id` is ON DELETE RESTRICT, so a topic is deletable only when it has no quizzes, and the flow branches on that before any dialog: **topic with no quizzes** — confirm dialog first ("Delete this topic?" / "This topic will be removed. This cannot be undone." — Delete destructive + Cancel), then "Topic deleted." in the strip; **topic with quizzes** — the delete is blocked (nothing is removed), no confirm dialog is shown, and the status strip reads "This topic still has quizzes. Delete its quizzes first." — never a raw SQLite error (P8 acceptance).

### 5.4 Quiz management (P8 — `QuizManageForm`)

- **Layout:** header "Quiz management"; toolbar: search ("Search quizzes…"), topic filter (§4.5 combo, "All topics" + one per topic), a visible "Status" group label (`Caption`, `TextPrimary`, 8px gap) behind the status **segmented control** (All / Draft / Approved — §4.9's naming rule), "New quiz" primary trailing; grid card: **Title** · **Topic** · **Difficulty** · **Questions** (count — the number of the quiz's questions, the same value §5.5's list shows; the "Edit questions" action below opens the editor, the column only displays the count) · **Status** (Draft/Approved badge §4.10 inline) · **Created** (the schema's `created_at` — no `updated_at` exists, F11); trailing action column: **Edit** · **Edit questions** (→ §5.5) · **Delete** — the P8 action set; in P13 a **Review** action joins it on Draft rows (→ §5.12, when that screen exists).
- **Quiz form:** modal card: **Title** (§4.4), **Topic** (§4.5 combo of existing topics), **Difficulty** (§4.9 segmented, Easy/Medium/Hard). Save/Cancel.
- **States:** **empty** — "No quizzes yet" + "Generate one with AI or create one manually."; **filtered-empty** — "No quizzes match your search." + "Clear filters" secondary; **loading/error** as §5.3.
- **Feedback:** "Quiz saved." / "Quiz deleted." in the strip; duplicate title — "A quiz with this title already exists in this topic." (R-2 #5). Delete flow — schema fact: `quiz_attempts.quiz_id` is ON DELETE RESTRICT, and only an unblocked delete cascades the quiz's questions and their choices (`questions.quiz_id` / `choices.question_id` CASCADE); the flow branches on attempts before any dialog: **quiz with no attempts** — confirm dialog first ("Delete this quiz?" / "Its questions will also be removed. This cannot be undone." — Delete destructive + Cancel), then "Quiz deleted." in the strip; **quiz with attempts** — the delete is blocked (nothing is removed), no confirm dialog is shown, and the status strip reads "This quiz has recorded attempts and cannot be deleted." — never a raw SQLite error (P8 acceptance). Status changes belong to §5.12 (Draft → Approved only, R-2 #4); approval requires at least one question, so a zero-question quiz cannot be approved (§5.12 disables Approve with the hint "Add at least one question before approving."). The Status column's badges are the P13-mandated surface (Draft/Approved always visible here).

### 5.5 Question editor (P8 — `QuestionEditForm`)

- **Layout:** a shell drill-in panel (§5 preamble) — opened from a quiz row's "Edit questions" action in §5.4 (that action cell follows §4.6's keyboard contract). It is never a window: no title bar, no default chrome, no duplicated title — the in-content header is the only title. Header: "Back" secondary (leading, the §5.9/§5.12 pattern) returns to the §5.4 quiz list with its search/filter state intact (§5.2's panel persistence); then "Questions — {quiz title}" (`Title`) + the quiz's status badge. While the editor is open the shell is unchanged (§5 preamble): the sidebar stays enabled, "Quiz management" stays the active nav item, nothing dims. Toolbar: "Add question" primary; questions list card. Each row: position number ("1", `Caption` `TextSecondary`), question text (`Body`, truncated to one line), kind tag ("Multiple choice" / "True–false", `Caption` `TextSecondary`), trailing controls: **up/down reorder glyph buttons** (§4.1 quiet secondary, chevron glyphs), Edit (secondary), Delete (destructive text).
- **Reordering (R-2 #8):** up is disabled on the first row, down on the last; a move swaps the two rows' positions in one transaction and the visible numbering renumbers 1…n automatically — positions are never manually typed, so they cannot collide.
- **Question form:** a state of the drill-in panel — the list card swaps for the form card; never a modal, never a window (§4.11's modals-never-navigate rule). Save returns to the list with the strip line; Cancel returns without saving. **Question text** (multi-line §4.4), **Type** (§4.9 segmented: Multiple choice / True–false — matching the P3 `Question` subclasses), **Choices** — four choice rows (multiple choice) or the fixed two (true–false): each row is a §4.7 CheckBox with its visible label "Mark choice 1 as the correct answer" … "Mark choice 4 as the correct answer" (true–false: choices 1–2; `Body` `TextPrimary` per §4.7's recipe, 8px gaps, clicking the label toggles the box — it is the source of §4.7's accessible name), followed by a §4.4 text input (placeholder "Choice text"); **Explanation** (multi-line §4.4, label "Explanation (optional)"). Save/Cancel.
- **States:** **empty list** — "No questions yet" + "Add the first question."; **loading/error** — as §5.3 (local reads are instant, so no spinner; read errors use the §4.11 unexpected dialog); form errors inline: "Enter the question text.", "Fill in every choice.", "Mark exactly one correct answer." (exactly-one rule, R-2 #3), "This choice text is already used for this question." (R-2 #5 duplicate-choice message).
- **Feedback:** "Question saved." / "Question deleted." strip lines; reorders give silent visual feedback (the renumbered list is the message); explanation saves as an empty string when left blank (R-2 #6 — the editor always supplies a value; §5.9/§5.12 display it).

### 5.6 Quiz-taking (P9 — `QuizTakeForm`)

**Moment 1 — the quiz list (home panel):**

- **Layout:** header "Take quiz"; one list card (§4.6): rows = **Title** · **Topic** · **Difficulty** · **Questions** · badge. Approved quizzes are selectable rows with a trailing "Start" primary-style row action; **Draft quizzes render as disabled rows** (`TextTertiary`, not selectable) carrying the Draft badge plus the caption "Pending review" (P9: Draft is listed but never startable — proposal line 23 enforced at the UI).
- **States:** **empty** — "No quizzes yet."; drafts-only — "Quizzes appear here once they're approved."; loading/error as §5.3.
- **Feedback:** none on entry; a just-completed attempt returns the user here with "Result saved." in the strip.

**Moment 2 — the run:**

- **Layout:** content header becomes the run header: quiz title (`BodySemibold`) + "Question 3 of 10" (`Caption`, `TextSecondary`); directly under it a **determinate** 8px progress track (§4.13 recipe's track, `Accent` fill, value = answered ÷ total — here progress is genuinely known, unlike generation); then the question card: question text (`BodySemibold`), 12px, the choice list — each choice a full-width row card (Surface, 8px radius, ≥36px, mark column + `Body` text); footer row inside the content area (not window-bottom, §3.3): "Previous" secondary (disabled on question 1), "Next" primary — on the final question the same button reads "Submit".
- **Choice selection:** tap selects — row gains `AccentTint` fill and the mark fills `Accent`; one selection at a time; tapping another moves it (no toggle-off; changing an answer is allowed until submit). Next/Previous never lose the stored selection for that question.
- **States:** the run always has content (approval requires ≥1 question, §5.4). Submitting disables the button for the duration of the one transaction — double submission cannot create a second attempt (P9 guard). Leaving mid-quiz via any nav item raises the §4.11 dialog: "Leave this quiz?" / "Your answers so far will be discarded." — Leave (destructive text) / Stay (secondary); Leave discards cleanly, no attempt row is written (documented P9 behavior).
- **Feedback:** on submit, scoring runs instantly and the shell swaps to §5.7 — the handoff itself is the feedback.

### 5.7 Result summary (P9)

- **Layout:** header "Result"; a hero card: the score in `Title` ("8 / 10") with "80%" (`Body`, `TextSecondary`) beside it and "Artificial Intelligence · Hard" as a `Caption` context line; below, a per-question list card: each row = the mark (check glyph + "Correct" in `SuccessText`, or cross glyph + "Incorrect" in `DangerText` — shape plus word, never color alone), "Question 3" (`Caption` `TextSecondary`), and the question text truncated (`Body`).
- **Actions:** "View answers" (secondary → §5.9 for this attempt), "Retake" (primary — restarts §5.6 moment 2 with the same quiz), "Done" (secondary → home list).
- **States:** always has data (the attempt was just scored and saved — one `QuizAttempt` + its `SubmittedAnswer` rows, one transaction). No empty/loading/error state exists by construction.
- **Feedback:** the screen itself is the success feedback; no dialogs.

### 5.8 History (P10 — `HistoryForm`)

- **Layout:** header "History"; toolbar: search ("Search quizzes…"), topic filter (§4.5 combo), score filter (§4.5 combo: Any score / 80% and above / 50–79% / Below 50%); grid card: **Date** · **Quiz** · **Topic** · **Score** ("8/10 (80%)"). Rows open §5.9 (double-click, Enter, or a trailing "Review" action).
- **States:** **empty** — "No quizzes taken yet" + "Take a quiz and your results will appear here."; **filtered-empty** — "No attempts match your filters." + "Clear filters"; loading/error as §5.3.
- **Offline note (proposal line 23):** this screen is fully local — it has no network dependency to design around; it must work with the cable unplugged (P10 acceptance).
- **Feedback:** read-only screen — no CRUD messages by design.

### 5.9 Attempt review (P10 — `AttemptReviewForm`)

- **Layout:** drill-in: header "{quiz title}" + "Back" secondary (leading) + the score line "8/10 · 80% · Nov 4, 2026" (`Caption`, `TextSecondary`); a vertical stack of question blocks, one card each:
  1. top line: "Question 3" (`Caption` `TextSecondary`) + the mark ("Correct" `SuccessText` / "Incorrect" `DangerText`, glyph + word);
  2. question text (`Body`);
  3. the answer rows — **the chosen answer** labeled "Your answer" (`Caption` prefix): marked with the SuccessText check if it was the correct choice, or with the DangerText cross if not; on incorrect questions the **correct choice** row additionally carries the label "Correct answer" with the SuccessText check (P10 acceptance: chosen vs correct always both visible);
  4. the explanation in a quiet inset box (`Fill` fill, 8px radius, 12px padding, "Explanation" `CaptionSemibold` label, text `Body`) — always rendered; when the stored explanation is the empty string the box reads "(No explanation was provided.)" (R-2 #6: P10 displays it either way).
- **States:** opened only from an existing attempt — no empty state by construction; a read error uses §4.11.
- **Feedback:** read-only.

### 5.10 Reports (P12 — `ReportsForm`)

- **Layout:** header "Reports"; filter row: topic filter (§4.5 combo, "All topics"), difficulty filter (§4.5 combo, "All difficulties"); **summary card** — three inline stat groups, each a number in `Title` over a `Caption` label: **Attempts** · **Quizzes taken** · **Average score** (shown as a percentage); 16px; **per-topic table card** (§4.6): **Topic** · **Attempts** · **Average %** · **Best %** · **Needs practice** (a quiet chip: `WarningTint` fill + "Needs practice" in `WarningText`, `CaptionSemibold` — text plus color, §2.6); 16px; **needs-practice list card**, header "Topics that need more practice" (`BodySemibold`): rows sorted **ascending by average score** (weakest first, per plan P12), each row topic name (`Body`) + average (`Body`) + attempts (`Caption` `TextSecondary`). The threshold flag comes from the P11 service; the UI only renders it.
- **States:** **empty (no attempts)** — the summary card, table, and list are all replaced by one friendly empty state: "No quiz attempts yet" + "Take a quiz and your reports will appear here." Zeros are never presented as data (P12 acceptance); **topic with zero attempts** — a defined empty row treatment: "—" in the numeric columns, no chip; loading/error as §5.3.
- **Feedback:** read-only; the panel reloads from `IReportService` each time it becomes visible, so finishing a quiz updates it on return.

### 5.11 AI generation (P13 — `GenerateQuizForm`)

- **Layout:** header "Generate quiz"; **options card**: Topic (§4.5 combo — every existing topic across both categories), Difficulty (§4.9 segmented: Easy / Medium / Hard), Questions (§4.12 stepper, 5–20); **action card** below: "Generate" primary button + the quiet note (`Caption`, `TextSecondary`): "Needs an internet connection. Generating usually takes up to a minute."
- **Busy state — the designed moment (R-5 #3, binding):** generation's worst case is ≈2 minutes (two 60 s timeout tiers), and the call runs **off the UI thread** so this state actually animates — a frozen window is a review defect, and the animation is the visible proof of the threading obligation:
  - the Generate button enters its disabled state (§4.1);
  - the action card swaps to the busy block: the §4.13 **marquee** track + "Generating quiz…" (`Caption`) + a live elapsed-seconds counter ("42 s"), ticking on the UI timer;
  - a "Cancel" secondary button appears beside the track;
  - the option controls disable and the sidebar items disable (§5.2) — one unambiguous state, no half-locked form;
  - **no percentage and no fake progress** — duration is unknowable; the promise is activity, not percent.
- **Cancellation:** "Cancel" returns the form to its ready state immediately. If the in-flight request can be aborted through the P5 client, it is; if not, the late result is discarded and a strip caption notes "A generation finished after you cancelled. It was not saved." The form never leaves the user waiting on an unresponsive window.
- **Success:** the quiz saves as **Draft** (P5 — AI quizzes are born Draft; nothing here auto-approves): strip line "Quiz generated and saved as a draft." + a "Review now" secondary action deep-linking to §5.12 with that quiz open.
- **Typed failure — the banner (offline/quota path):** on any contract error class the form shows an inline **warning banner** at the top of the options card — `WarningTint` fill, 8px radius, 12px padding: a title line (`BodySemibold`, `WarningText`) "Quiz generation is unavailable right now." + a detail line (`Body`, `TextPrimary`) carrying the typed reason in plain language — no provider configured: "No AI provider is configured."; auth failure: "The AI provider rejected the request. Check the provider configuration."; quota/rate limit: "The AI provider reached its request limit. Try again later."; network/timeout: "Couldn't reach the AI provider. Check the internet connection." — and always the **pointer at the offline path** (plan P13): "You can still take any quizzes already saved on this computer." Actions row: "Try again" (secondary) and "Browse quizzes" (secondary → §5.6 moment 1). The app never crashes and never half-saves a quiz (P13 acceptance).
- **States:** **empty (no topics)** — "Create a topic first." + "Go to topics" secondary; busy and failure states as above; there is no loading state distinct from busy.

### 5.12 Quiz review / approve (P13 — `QuizReviewForm`)

- **Layout — list view:** header "Review" + the explaining caption "AI-generated quizzes are checked here before they can be taken." (`Body`, `TextSecondary`); list card (§4.6): **Title** · **Topic** · **Difficulty** · **Questions** · **Generated** · Draft badge (§4.10); trailing "Review" primary row action.
- **Layout — review view (drill-in):** header "{quiz title}" + Draft badge + "Back" secondary; then the AI's questions as blocks (one card per question, §5.9's rhythm): question text (`Body`), the four choice rows with **the AI's claimed correct answer** marked — `AccentTint` fill + the label "AI's answer" (`Caption`) on that row (the claim is highlighted, not validated), then the explanation inset box ("Explanation" — the AI's claimed reasoning, always rendered, "(No explanation was provided.)" when empty, R-2 #6).
- **Actions (review view, header-trailing row):** "Approve" (primary — flips status Draft → Approved only, R-2 #4: no audit fields exist or are displayed); "Edit questions" (secondary → §5.5 for this quiz — the reviewer's correct-it path); "Discard" (destructive → §4.11 confirm: "Discard this quiz?" / "The quiz and all its questions will be permanently deleted." — Discard destructive / Cancel).
- **States:** **empty** — "No quizzes to review" + "Generated quizzes appear here as drafts."; zero-question quiz — Approve is disabled with the inline hint "Add at least one question before approving."; busy/error as §5.3.
- **Feedback:** "Quiz approved." strip line; the badge flips to Approved in place (the state change is the moment — no dialog, no animation); after approval the quiz appears takeable in §5.6 (P13 acceptance, verified in the P9 flow). Draft quizzes from this screen never appear startable anywhere.

## 6. The unslop bar (acceptance checklist)

This section is the acceptance checklist P16 audits every screen against. Each claim is a **negative assertion about this design**: the pattern does not exist in any §2–§5 recipe, and implementers cannot reintroduce it without a spec change.

| # | Sign | Assertion — where the design forecloses it |
|---|---|---|
| 1 | **No purple gradients (or any gradients)** | Asserted. One accent exists: `Accent` #0088FF blue (§2.1). Every fill in §4 is a flat GDI+ color; the §4 preamble bans gradients outright, and §3.6 adopts "never gradient backgrounds" as a ban. |
| 2 | **No glows** | Asserted. Focus is a 1px border color swap or a 1px outline (§4.3/§4.4); no painted glow, no outer halo, no luminous anything. |
| 3 | **No gradient text** | Asserted. Text renders in exactly the §2.1 single-color text tokens; §2.2 defines no text effect. |
| 4 | **No emoji labels** | Asserted. Every string in §5 is plain language; iconography follows §3.7 — text labels by default, Segoe MDL2 glyphs only where one earns its place: nav marks, correct/incorrect marks, and the small in-control glyphs (drop chevron §4.5, column sort indicator §4.6, stepper −/+ §4.12, reorder chevrons §5.5); no external icon assets, no emoji. Feedback copy ("Save", "Try again", "No quizzes yet") contains none. |
| 5 | **No uppercase tracked labels** | Asserted. §2.2 defines sentence-style labels with no letter-spacing; nothing in §4 draws uppercase-transformed or tracked text. |
| 6 | **No glossy blobs / floating shapes / aurora** | Asserted. The design contains zero decorative shapes — every element is a functional control on the §2.1 surfaces; §3.6's tell list (orbs, particles) is adopted as a ban. |
| 7 | **No fake or looping motion** | Asserted. One animated element exists in the entire app: the P13 marquee, which runs only while a real generation call is in flight and stops the moment it returns (§4.13). No bounce, no fade-up, no pulse, no spinner decoration; state changes are instant paint changes. Stillness is the default; motion must prove real work. |
| 8 | **No drop shadows on every card** | Asserted. Cards separate by surface contrast; the modal dialog has no shadow either (§4.11); §3.6's "borders instead of shadows" rule is baked into §4.2. |
| 9 | **No one-radius-everywhere** | Asserted. §2.4 fixes 12/8/4 by element class — the tell of a single radius applied to all elements cannot occur. |
| 10 | **No colored left-border panels** | Asserted (the strongest single AI tell per the ui-reference catalog). No recipe in §4 draws an accent left edge on any card, row, or panel; emphasis is background tint, weight, or a badge. |
| 11 | **No default blue selection chrome** | Asserted. Grid selection is `AccentTint` + `TextPrimary` (§4.6); the WinForms native highlight is disabled wherever a list renders. |
| 12 | **No shouty or inconsistent copy** | Asserted. Empty states follow one pattern ("No topics yet", "No quizzes yet", "No attempts…"), buttons are specific verbs, titles describe the situation, and §4.11 fixes tone rules (no exclamation marks, no "Error" titles, no "Yes"/"No"). |

**Stillness statement:** the app is still by design. It earns its polish from spacing, hierarchy, and consistent tokens — not from movement. WinForms supports exactly two clean subtle state changes — instant fill/border swaps and the marquee — and this spec uses only those two. P16 screens every form against this table; any sign found is a defect to fix, not a style choice to defend.

## 7. Binding obligations surfaced in design

Traceability from the plan's rulings and backlog to the design decisions that carry them — so a reviewer can verify each obligation has a designed home.

| Source | Obligation | Designed in |
|---|---|---|
| R-2 #7 | Category is a two-value dropdown (`Programming`/`Cybersecurity`), never free text; adding a category is a schema-change phase | §4.5 (`DropDownStyle = DropDownList`, exactly two items), §5.3 topic form |
| R-2 #5 | UNIQUE violations surface as friendly duplicate messages | §5.3 "A topic with this name already exists."; §5.4 "A quiz with this title already exists in this topic."; §5.5 "This choice text is already used for this question." |
| R-2 #8 | `position` unique per parent; stable ordering via transaction | §5.5 — up/down reorder controls only (positions never typed), end-of-list buttons disabled, renumber automatic |
| R-2 #4 | Draft → Approved flip only; no audit columns | §5.12 "Approve" action; badges (§4.10) are the only status UI on §5.4/§5.6/§5.12 |
| R-2 #6 | `explanation NOT NULL` — editor always supplies, P10/P13 display | §5.5 (empty string on blank), §5.9 and §5.12 inset boxes with "(No explanation was provided.)" |
| R-2 #3 / P8 | Exactly one correct choice per question | §5.5 error "Mark exactly one correct answer." |
| R-2 #1 | No stored derived columns; correctness derived live | §5.9 renders chosen-vs-correct from the attempt's joins — the design displays no stored `is_correct` |
| R-5 #3 / R-3 (P13 threading note) | Generation invoked off the UI thread; busy indicator animates; UI responsive and cancellable-feeling | §4.13 (marquee + elapsed counter + Cancel) and §5.11 (busy state as a designed moment; a frozen window is a review defect) |
| R-5 #3 (typed failure) | Offline/quota failure shows the typed reason pointing at the offline path | §5.11 warning banner with the plain-language reason map and "Browse quizzes" action |
| Backlog #1 | Answer-key review before availability (proposal line 23) → Draft/Approved workflow | §5.12 review/approve screen; Draft is never startable (§5.6) |
| Backlog #5 | Users table gets a purpose: minimal login | §5.1 login screen; single seeded user per plan assumption #1 |
| Backlog #11 | Feature list resolved from prose before design | §1 — stated resolution; `[TABLE 1]` disregarded |
| P8 e2e | FK-protected delete shows a friendly message, never raw SQLite | §5.3 "This topic still has quizzes. Delete its quizzes first." |
| P16 | The five-line direction | Its checklist is restated item-by-item as §6; P16 audits against that table |
| P8 ux-review F1 (P6.1) | Grid row actions keyboard-operable (WCAG 2.1.1) — every action reachable and activatable by keyboard, visible focus | §4.6 keyboard-operability contract (arrows/Tab reach, Enter/Space activate, §4.1 ring, row action announcement + grid hint) |
| P8 ux-review F2 (P6.1) | Custom controls expose role/name/state (WCAG 4.1.2); accessible name equals the visible label | §4.7/§4.9/§4.10 accessibility contracts (`AccessibleObject`/`CreateAccessibleInstance` overrides); §4.4 label↔input naming |
| P8 ux-review F3 interim (P6.1) | Strip verdict text meets AA before the R-D3 re-issue | §2.6 interim bullet + §4.11 interim strip token (`TextPrimary` on `Background`) — clearly marked interim, discarded by R-D3 |
| P8 ux-review F5 (P6.1) | The question editor is a shell drill-in panel, never a default window | §5 preamble drill-in rule; §5.5 layout; §4.11 modals-never-navigate |
| P8 ux-review F11 (P6.1) | Grid columns match the schema (`created_at` only); the §5.5 path exists in P8 | §5.4 — "Created" column, "Edit questions" action, "Questions (count)" disambiguation |

## 8. Implementation handoff

- **`UITheme.cs` (P7)** carries §2 verbatim as constants: the color tokens (§2.1 names are the constant names), the three type styles plus the two weight variants (§2.2), the spacing steps (§2.3), and the radii (§2.4). Every custom control in §4 reads from it — no hard-coded colors or sizes in forms.
- **Custom controls implied by §4** (implemented by the Frontend agent as small classes, no code in this spec): flat button (three variants), card panel, nav item, segmented control, status badge, restyled input, iOS-list `DataGridView` wrapper, modal dialog form, numeric stepper, busy block. One implementation per recipe, reused on every screen — the consistency the rubric scores is structural, not per-form effort.
- **Copy is canonical.** The message strings quoted in §4.11 and §5 are the design of record's wording; P14's sweep keeps implementations true to them (same pattern, same tone) rather than inventing variants.
- **Out of scope for this spec:** behavior and business rules (scoring math, validation logic, persistence order — they live in their phases), database naming, and anything in `src/` — this document prescribes what the UI looks like and says, not how layers are wired. Deviating from this spec is a design change: route it back through the Orchestrator to the Designer rather than improvising at implementation time.
