# D4 — Dark-Theme Re-issue & Gamification Amendment (WP2)

**Status:** approved amendment to `designs/codequest-ui-spec.md` (Lead decisions bound, 2026-11-08) · **Author:** UX/UI Designer · **Date:** 2026-11-08 · **Owner directives carried:** dark-only app; XP/levels; Scoreboard; Achievements; AI topic dialog; result reveal; anti-slop (shadow/border/typography/spacing).

**Lead decisions on the designer's open questions (binding):**
1. Nav order: Scoreboard + Achievements directly after "Take quiz" — **approved**.
2. Grid header ink `AccentText` — **approved**.
3. Result reveal fade — **none** (the reveal is an instant paint change; no fade ships).
4. Achievements screen has no empty state by construction — **confirmed**.
5. History score pill defect fix (wash fill + `Level*` text) — **approved** for both themes.
6. Chip recipe numbered §4.14 — **approved**.
7. Tile hover mechanism (per-tile blend-15%-toward-black constants replacing `ControlPaint.Dark`) — **approved**.

**Method note:** every ratio below was computed with the WCAG relative-luminance formula (sRGB linearization). Floors: **4.5:1** normal text, **3:1** large/bold text (≥18 pt regular / ≥14 pt bold) and non-text control boundaries, **EXEMPT** for placeholder/disabled roles, **—** for decorative values with no floor. Segoe MDL2 codepoints verified against Microsoft Learn, "Segoe MDL2 Assets icons" (fetched 2026-11-08).

---

## 1. §2.1 amendment — D4 dark token re-issue (complete table)

The palette becomes **dark neutral surfaces, one indigo accent, three semantic colors**. Token *names and structure* are unchanged — only values re-issue — so `UITheme.cs` remains the single code revision point. The §2 preamble sentence "Dark mode is out of scope: the app ships one light appearance" is **superseded by owner direction (dark-only)**.

### 1.1 Surfaces and neutrals (elevation ramp)

| Token | Hex | Used for | Key ratios | Floor | Verdict |
|---|---|---|---|---|---|
| `Background` | `#101216` | Window, sidebar, content background | — (base) | — | Plan value, kept |
| `Surface` | `#171A20` | Cards, list containers, input fields, grids | vs Background **1.08** | — (decorative) | Plan value, kept |
| `SurfaceRaised` *(new)* | `#1E222B` | Dialogs, combo dropdown panels — anything floating | vs Surface **1.09**, vs Background **1.18** | — | Plan value, kept |
| `Fill` | `#232833` | Hover/selected row fill, pressed chips, quiet control fills, progress track, disabled inputs | vs Background 1.27, vs Surface 1.18 | — | Plan value, kept |
| `FillPressed` | `#2A303D` | Secondary button pressed, one step above Fill | vs Fill 1.12 | — | Plan value, kept |
| `SegmentHover` *(new)* | `#2C3140` | Unselected-segment hover (§4.9) | vs Fill 1.14 | — | New (Plan had none); mirrors light's quiet hover step |
| `BorderControl` | `#646E82` | Text-input/combo borders, unchecked control rings, stepper | vs Surface **3.40**, vs SurfaceRaised **3.10**, vs Background 3.66 | **3.0 non-text** | **Re-issued** — Plan's `#4A5264` measures **2.22:1** on Surface and fails its own ≥3:1 annotation |
| `BorderHairline` *(new)* | `#323845` | 1px edges of passive containers: cards, dialog card | vs Surface 1.48, vs SurfaceRaised 1.36, vs Background 1.60 | — (decorative, see border rule) | New; the dark replacement for light's contrast-only card separation |
| `SeparatorHairline` | `#2A2F3A` | 1px row separators (§2.5), sidebar right edge | vs Surface 1.30 | — | **Adjusted** from Plan `#262B36` (1.23) to match the light theme's perceived line weight (light `#D8D8DC` on white = 1.42) |

**Elevation rule:** exactly three levels — `Background` → `Surface` → `SurfaceRaised`, each step lighter (1.08 / 1.09 — deliberately the same subtlety as light's white-on-`#F5F5F7` 1.09). A container nests one level per float: window/sidebar → cards → dialogs/dropdowns. Never skip a level (no Raised card on Background, no Surface dialog). **State fills are not elevation** — `Fill`, `FillPressed`, `SegmentHover`, `AccentTint`, and the washes always paint on top of the current level. On dark, neutral fills **lighten** one step per interaction state (hover → pressed); the accent family **darkens** (it is already the brightest element).

**Border rule:**
- **`BorderControl`** — the only borders with an AA floor (3:1, met on all three surfaces): 1px borders of *interactive controls* — text inputs, combos, numeric stepper, unchecked checkbox/radio rings.
- **`BorderHairline`** — 1px edges of *passive containers*: every card (§4.2), the dialog card. Decorative by classification (the container is also identified by elevation and content); chosen ≥1.36:1 against every surface so it is perceivable without weight.
- **`SeparatorHairline`** — 1px row separators *inside* one container (§2.5) and the sidebar's right edge (§5.2).
- **None** — buttons (fills do the work), chips/badges, tiles, hover/selected states. The dialog is the one element with both a hairline and a shadow (owner-mandated; see §2 below).

### 1.2 Text

| Token | Hex | Role | On Surface | On Background | On SurfaceRaised | Floor | Verdict |
|---|---|---|---|---|---|---|---|
| `TextPrimary` | `#E8EAF0` | Primary content | **14.49** | **15.59** | **13.24** | 4.5 | Plan, kept |
| `TextSecondary` | `#9BA3B4` | Metadata, captions, quiet labels | **6.88** | **7.40** | **6.29** | 4.5 | Plan, kept (also 5.83 on Fill, 5.12 on SegmentHover) |
| `TextTertiary` | `#6B7385` | Placeholders, disabled text, decorative hints (**AA-exempt roles only**) | 3.66 | 3.94 | — | EXEMPT | Plan, kept (3.10 on Fill for disabled input text) |
| `TextOnAccent` | `#FFFFFF` | Text/icons on filled accent, filled danger, and the four answer tiles | on `Accent` **4.86**, `AccentHover` **6.25**, `AccentPressed` **8.07**, `Danger` **4.99**, `DangerHover` **6.42**, `DangerPressed` **8.27** | | | 4.5 (button labels are Body 10 pt = small text) | kept |

Rule carried from §2.6, re-pinned: white text appears **only** on `Accent`/`AccentHover`/`AccentPressed`, `Danger`/`DangerHover`/`DangerPressed`, and the four tiles/washes — never on `Fill`, `Surface`, tints, or washes.

### 1.3 Accent — one, and indigo (`Accent` is a FILL; `AccentText` is its ink)

| Token | Hex | Used for | Key ratios | Floor | Verdict |
|---|---|---|---|---|---|
| `Accent` | `#6C5CE7` | Primary action fill, progress fills, focus-ring fallback — **fill only** | boundary vs Surface **3.59**, vs SurfaceRaised **3.28**, vs Background **3.86**; as progress fill vs Fill track **3.04** (tightest pass in the system — do not lighten `Fill` without re-checking) | 3.0 non-text | Plan, kept |
| `AccentHover` | `#5A4BD1` | Pointer hover on filled accent | white label **6.25** | 4.5 | Plan, kept |
| `AccentPressed` | `#493BB8` | Button while held | white label **8.07** | 4.5 | Plan, kept |
| `AccentDisabled` | `#262938` | Disabled filled buttons, label `TextTertiary` | label 3.03 (EXEMPT); fill vs Surface 1.21 | EXEMPT | **Re-issued** — Plan's `#3F3B5E` with the recipe's white label measures **14.42:1**, i.e. it reads as an *enabled* button. Disabled must read as disabled: dim indigo well + tertiary label |
| `AccentText` *(new)* | `#A79BF5` | **Everywhere light used `Accent` as ink**: links, secondary-button labels, grid column headers, nav active glyph, chips, the XP line, default focus ring | on Surface **7.18**, Background **7.72**, SurfaceRaised **6.56**, AccentTint **5.83**, Fill **6.08**, NavItemPressed **6.20**, FillPressed **5.45** | 4.5 | Plan's value, **validated**. Necessary: `Accent` as text on dark measures only **3.59** (Surface) / **3.86** (Background) — below the 4.5 small-text floor (it passed at 4.86 on light white) |
| `AccentTint` | `#2A2749` | Selected nav item, selected grid row, chips, unlocked achievement tiles | vs Background 1.33, vs Surface 1.23 (selection fills — decorative; selection is also weight + ink, never color alone) | — | Plan, kept |
| `NavItemPressed` | `#262244` | Nav item while held | vs Background 1.25; `AccentText` ink on it **6.20** | — | Plan, kept |

**General dark rule this table encodes:** wherever the light theme painted `Accent` as *ink* (text, glyph, outline, focused border swap), dark paints **`AccentText`**. `Accent` remains a *fill* only. Same relationship as `Danger` (fill) vs `DangerText` (ink).

### 1.4 Semantic colors (fills + dark-wash tints + text)

| Token | Fill | Text | Tint (wash) | Text ratios | Verdict |
|---|---|---|---|---|---|
| Success | `#2FA857` | `#4CC27A` | `#16301F` | text on Surface **7.72**, Background **8.31**, Raised **7.06**, on tint **6.30**; fill as mark vs Surface **5.69** | Plan, kept |
| Danger | `#CF3530` | `#F07A72` | `#3A1A18` | white on fill **4.99**, on hover `#B32A24` **6.42**, on pressed `#97201B` **8.27**; text on Surface **6.42**, Background **6.90**, Raised **5.86**, on tint **5.77** | **Fill re-issued** from Plan `#C62B25`: it measured **2.86:1** vs SurfaceRaised, so a destructive button in a dialog failed the 3:1 boundary floor. `#CF3530` = 3.19 (Raised) / 3.49 (Surface), white 4.99. **Text/tint kept** (Plan) |
| Warning | `#B07C10` | `#E5A83B` | `#38290F` | text on Surface **8.29**, Background **8.92**, on tint **6.70**; fill vs Surface **4.76**, banner border on tint **3.84** | Plan, kept |

**New dark state colors** (Plan had none for danger; derived darken steps consistent with `AccentHover/Pressed`):

| Token | Hex | Used for | Ratio | Verdict |
|---|---|---|---|---|
| `DangerHover` | `#B32A24` | Destructive hover | white **6.42** | New |
| `DangerPressed` | `#97201B` | Destructive held | white **8.27** | New |
| `DangerDisabled` | `#2E2325` | Disabled destructive, label `TextTertiary` | 3.19 (EXEMPT) | New |
| `SuccessText`/`DangerText`/`WarningText` on `Background` (status-strip verdict lines) | — | §5 preamble strip | **8.31 / 6.90 / 8.92** | The F3 interim all-`TextPrimary` strip rule is **definitively discarded** — semantic verdict tokens meet AA on the dark strip |

Tints are dark washes (Plan rule, kept): banner/badge backgrounds, never text.

### 1.5 Fun palette (learner surfaces only — tiles, difficulty, washes)

Tile labels are the choice text in `TextOnAccent` at `BodySemibold` 10 pt — **small text by WCAG** (large = ≥14 pt bold), so the floor is **4.5:1**, not the 3:1 "large text" reading the light palette assumed. All four tiles are re-pinned to clear 4.5:

| Token | Hex (dark) | White label | vs Surface / Background | Verdict |
|---|---|---|---|---|
| `FunPrimary` | = `Accent` | **4.86** | 3.59 / 3.86 | Alias kept |
| `TileRed` | `#D63040` | **4.82** | 3.61 / 3.89 | **Kept at the light value** — Plan's brighter `#E14B5A` measures 3.92 and fails 4.5 |
| `TileBlue` | `#2D6BE4` | **4.84** | 3.60 / 3.87 | Kept at light value — Plan's `#3D7BF0` measures 3.97, fails 4.5 |
| `TileAmber` | `#A65F00` | **4.93** | 3.53 / 4.01 | **Re-issued** — Plan's `#E09413` measures **2.50:1**, failing even the 3:1 floor; `#A65F00` is the brightest amber that clears 4.5 |
| `TileGreen` | `#178146` | **4.92** | 3.54 / 3.81 | **Re-issued** — Plan's `#27A45C` measures 3.21, fails 4.5 |
| `CorrectWash` | `#178146` | **4.92** | 3.54 / 3.81 | = TileGreen, mirroring the light relationship |
| `WrongWash` | `#B3241F` | **6.59** | 2.65 (decorative) | Light value kept |
| `LevelEasy` | `#4CC27A` | — | bar fill vs Fill track **6.54**; chip text on `SuccessTint` **6.30** | **Re-issued as the *text-variant* greens/ambers/reds** (dual-use: difficulty chip text on its wash + score-bar fill on the track) |
| `LevelMedium` | `#E5A83B` | — | bar fill **7.03**; on `WarningTint` **6.70** | same |
| `LevelHard` | `#F07A72` | — | bar fill **5.43**; on `DangerTint` **5.77** | same |

**Consequence flagged as a found defect (both themes):** `HistoryForm.cs` (lines 186–190, 235–246) paints the score pill as **white `TextOnAccent` on a `Level*` fill** — on the dark values that is white on `#4CC27A` = **2.26:1**, `#E5A83B` = **2.10:1**, `#F07A72` = **2.72:1** (and in the *current light* theme white on `#1E8E4E` is 4.17:1 — already failing). Fix prescribed in §5 checklist: pill becomes wash fill + `Level*` text (6.30/6.70/5.77 ✓).

**Stale-comment correction for WP3:** `UITheme.cs`'s fun-set comment claims white-on-tile = "Red 4.3, Blue 4.6, Amber 7.4, Green 4.0". Actual: **4.82 / 4.84 / 3.46 / 4.17** (the amber claim is off by 2×). Correct the comments while re-issuing.

### 1.6 "Color has a job" — restated for dark

Every token signals state or action; none is decoration. `Accent`/`AccentText` appear only where a thing is the **primary action or the current selection** (plus the XP line/level fills — the gamification identity the owner requested). Semantic colors appear only for **verdicts** (correct/incorrect, needs-practice, blocked-generation banners, difficulty chips). State is never color alone: badges carry labels, verdicts carry glyph + word, the signed-in row carries weight + a "You" chip. Unlocked achievements are a **possession** state → accent family, deliberately **not** Success-green (success stays a quiz verdict; ten green cards would be semantic color as decoration).

---

## 2. §2/§4 amendment — Shadow, border, typography, spacing (the anti-slop pass)

**Cards (amends §4.2, `CardPanel`):** `Surface` fill, `RadiusCard` 12, **+ 1px `BorderHairline` edge on all four sides** — on dark the hairline replaces the light theme's contrast-only separation (Surface vs Background is 1.08:1 in both themes, but dark reads flatter, so the border does real work). **No gradients, no glow, no painted shadows on cards — ever.** `CardPanel` gains the border pen; the `Region` clip stays. A card never carries both a border and a shadow.

**Dialogs (amends §4.11, `DialogForm`):** fill becomes **`SurfaceRaised`** (the one floating level), edge becomes **1px `BorderHairline`** (the pen at `DialogForm.cs:233` swaps token), and the dialog gains the **native `CS_DROPSHADOW`** via a `CreateParams` override — the *only* shadow in the app, owner-mandated, platform-drawn (no GDI+ shadow painting). No dimmed backdrop (flat modal, unchanged). §6 sign 8 is amended accordingly: "no drop shadows **on cards**; the modal dialog is the single, native, owner-mandated exception."

**Typography (§2.2 — unchanged, restated):** one family, Segoe UI; three sizes, emphasis by weight at constant size, no fourth size ever:
- `Title` (Semibold 16 pt) — screen titles in the content header; the result score; the Scoreboard's total-XP number.
  - **Lead ruling (2026-11-08):** the result score keeps the fun-UI `Display` token (Segoe UI Bold 28 pt) — the owner's "more fun and entertaining" directive introduced it after the base spec, and this restatement did not account for it. `Title` remains the Scoreboard total-XP style.
- `Body` (Regular 10 pt) — everything else by default: rows, cells, inputs, secondary labels, descriptions.
- `Caption` (Regular 9 pt) — metadata, hints, quiet group labels, progress captions ("70 XP to Lvl 4"), explaining captions.
- `BodySemibold` — row/card titles, primary-button labels, the quiz question, form field labels, the signed-in player's name, the "+120 XP" line, achievement titles.
- `CaptionSemibold` — badge/chip text ("Lvl 3", "You", achievement chip titles, banner title lines stay `BodySemibold` per §5.11).
On dark, hierarchy is carried by the same scale **plus the ink ramp** (`TextPrimary`/`Secondary`/`Tertiary`) — never by inventing sizes or weights. Glyph fonts: `NavGlyph` 12 pt (nav) and one new display instance `AchievementGlyph` (Segoe MDL2 Assets 16 pt) for the 40px achievement tiles — icons, not text sizes.

**Spacing (§2.3 — unchanged):** the six steps 4/8/12/16/24/32 only. Gap semantics for the new screens: **4** icon↔label inside chips/tiles; **8** control↔control inside a group, chip internals, reveal sub-lines; **12** list-row vertical padding, input vertical padding, tile text inset; **16** screen margin, card↔card (summary card ↔ table card, achievement card ↔ achievement card, field↔field in the AI dialog), reveal block gap; **24** section gap (toolbar↔grid), dialog content inset; **32** content-header ↔ content. When in doubt, take the larger step.

**State restraint (§2.1/§6 — restated + extended):**
- Accent (fill or `AccentText` ink) on **one primary action per region**, the **current selection** (nav item, grid selection, focused control), and the **gamification progress** (XP line, level bars). Never on two competing buttons, never on passive text, never on rank numbers, never on locked tiles.
- Semantic colors only for verdicts and blocked states. No success-green celebrations, no red unless something is wrong or destructive.
- **Tile hover on dark** (amends §5.6/`AnswerTile`): fills may not lighten (white labels would drop below 4.5 — TileRed has only ~9% headroom). Hover = **fill blended 15% toward black** (explicit per-tile constants in `UITheme`, replacing `ControlPaint.Dark(tile, 0.08f)`); darkening raises white-label contrast (≥6.2) and is visible. Selection stays the 2px `TextOnAccent` ring (≥4.82 on every fill and wash — validated).
- No decorative color anywhere: no rank medals, no per-achievement rainbow, no colored card edges (§6 sign 10 stands).

---

## 3. New screen recipes (§4/§5 amendment sections)

> Numbering note: the brief suggested "§4.12 sidebar level chip", but §4.12 is the numeric stepper and §4.13 the progress bar; the chip is filed as **§4.14** (Lead-approved).

### §4.14 Sidebar level chip (amends §5.2 account block; reused by §5.13/§5.7a)

- **Anatomy:** inside the account block, 16px below the nav list: row 1 = signed-in display name (`Caption` `TextSecondary`, leading) + **level chip** (trailing); 8px below: **progress bar** spanning the sidebar content width (220 − 2×24 = 172px standard-DPI), **4px tall** (thin variant of the §4.13 pill), track `Fill`, fill `Accent` (vs track **3.04** ✓). 16px below: "Log out" nav item (unchanged).
- **Chip recipe:** 20px tall, `RadiusChip` 4, 8px horizontal padding, `CaptionSemibold` text `Lvl N`, `AccentTint` fill, `AccentText` text (**5.83** ✓).
- **Math:** progress = (XP − threshold(n)) ÷ (threshold(n+1) − threshold(n)); thresholds 25(n−1)(n+2) — L2@100, L3@250, L4@450, L5@700, L6@1000. Example: 380 XP → Lvl 3, bar at 38%, caption not shown in sidebar (the caption lives on Scoreboard/result). Level 1 at 0 XP: empty track, "Lvl 1".
- **States:** static — not a control: no hover, no focus, no tab stop. Role `StaticText`, name **"Level 3"**; the bar exposes accessible name/value "380 of 450 XP to level 4". Values repaint on `IModulePanel.RefreshData` and after attempt submit; a level-up is an instant paint swap (no animation — §6 stillness).
- **One implementation**, reused by the Scoreboard summary card and the result reveal — never a second chip design.

### §5.2a Navigation amendment

Two entries join the nav list immediately after "Take quiz" (the learner cluster): **Scoreboard** — glyph `E8BC` **ShowResults**; **Achievements** — glyph `E735` **FavoriteStarFill**. Full order: Take quiz · Scoreboard · Achievements · History · Reports · Generate quiz · Topics (Review and Quiz management are not in the current nav). All §4.3 states apply; **on dark the active item's glyph and any accent ink render `AccentText`** (`Accent` glyph on `AccentTint` measures 2.91 — below the 3:1 non-text floor). Existing glyphs verified against MDL2: E80F Home, E823 Recent, E9D9 Diagnostic, E710 Add, E8EC Tag. New panels are persistent `ModulePanel`s that reload on show (Plan §5); names per Plan: `ScoreboardForm`, `AchievementsForm`.

### §5.13 Scoreboard (P-gamification — `ScoreboardForm`)

- **Layout:** header "Scoreboard" (`Title`) — read-only screen, no trailing primary action; status strip per §5 preamble (verdict lines in `SuccessText`/`DangerText` on `Background` — 8.31/6.90 ✓). Content inset 24px.
- **Personal summary card** (§4.2 + hairline, 16px internal inset), 16px above the table card: leading block = display name (`BodySemibold` `TextPrimary`) + "Lvl 3" chip (§4.14) with an 8px gap; 8px below: 8px progress track (`Fill`) with `Accent` fill + "70 XP to Lvl 4" (`Caption` `TextSecondary`) right-aligned on the same row. Trailing block: total XP in `Title` over "total XP" (`Caption` `TextSecondary`). With 0 XP the card still renders (real data): "Lvl 1", empty track, "100 XP to Lvl 2".
- **Leaderboard table card** (§4.6 grid, no action column): columns **Rank · Player · Level · XP · Quizzes**. Typography: headers `Caption` in **`AccentText`** (§4.6's quiet-header slot; the fun-accent header treatment re-issued with the readable ink — 7.18 ✓; Lead-approved); cells `Body` `TextPrimary`; **Rank / XP / Quizzes right-aligned, Player / Level left-aligned**. Row height 36, 1px `SeparatorHairline` separators inset 16px (§2.5), alternating rows off, sort indicator per §4.6. Rank = 1-based position; ordering XP desc, tie → more quizzes, tie → name (FR3) — display only.
- **Signed-in row highlight:** `AccentTint` full-row fill + player name in `BodySemibold` + a 20px "You" chip (`RadiusChip` 4, `Surface` fill, `CaptionSemibold` `AccentText` — 7.18 ✓) trailing the name. Triple-coded (fill + weight + label), never color alone.
- **States:** **empty** — centered panel in the card: "No quizzes taken yet" (`Body` `TextPrimary`) + "Take a quiz and the scoreboard will fill in." (`Caption` `TextSecondary`); **loading** — none (local SQLite reads are instant); **read error** — §4.11 unexpected dialog.
- **Interaction:** read-only — no action column, **selection disabled**, grid not a tab stop; the §4.6 action-hint line does not apply (it belongs to action grids). Screen readers get the cell text.

### §5.14 Achievements (P-gamification — `AchievementsForm`)

- **Layout:** header "Achievements" (`Title`) + explaining caption "Unlocks are recorded as you play." (`Body` `TextSecondary` — the §5.12 pattern). Content inset 24px. A wrapping grid of achievement cards: each card **240×120** standard-DPI (grid-friendly), **16px** card↔card gaps, 16px internal inset, §4.2 + hairline.
- **Card anatomy:** row 1 = **glyph tile** 40×40 (`RadiusControl` 8 rounded square) + achievement title (`BodySemibold` `TextPrimary`) beside it, 8px gap, vertically centered; 8px; description (`Caption` `TextSecondary`, two lines max); 8px; status line (`Caption`).
- **Unlocked state:** tile = `AccentTint` fill + the achievement's own glyph (Segoe MDL2, `AchievementGlyph` 16 pt, `AccentText` — 5.83 ✓); status line "Unlocked Nov 8, 2026" (`Caption` `TextSecondary`). Date = first unlock timestamp (A5: re-completing never duplicates, so the date never changes).
- **Locked state:** tile = `Fill` fill + **Lock glyph `E72E`** (`TextTertiary`); title `TextSecondary`; description **stays `TextSecondary`** (it tells the player what to do — it must be readable, 6.88 ✓); status line "Locked" (`Caption` `TextTertiary`). No card-level opacity tricks, no lock overlay, no recolor — the ink ramp does the muting.
- **Glyph assignments (all verified real MDL2 names/codepoints):** first-quiz `E768` Play · perfect-run `E73E` CheckMark · perfect-hard `E930` Completed · 10-quizzes `E7BC` ReadingList · 30-quizzes `E8F1` Library · 50-correct `E8FB` Accept · level-5 `E870` SignalBars5 · level-10 `E945` LightningBolt · 3-distinct-topics `E81E` MapLayers · both-categories `EA86` Puzzle. (Note: **MDL2 has no "Trophy"** — verified by search; `E734` is FavoriteStar and was rejected for the nav in favor of the filled star `E735`.)
- **States:** **no empty state exists by construction** — the catalog is code-defined; all ten cards always render, locked until earned (mirrors §5.7's "no empty state by construction"). Read error → §4.11. **No per-card interaction:** cards are display-only — no hover, no focus ring, no tab stops; accessible semantics `StaticText` with name = title text (date/description as description text).

### §5.3a AI New-topic dialog (reworks §5.3's topic form; the Edit-topic flow is unchanged and stays manual)

- **Shell:** one §4.11 modal card — `SurfaceRaised`, 12px radius, 1px `BorderHairline`, native `CS_DROPSHADOW`; min width 480; content inset 24; title "New topic" (`BodySemibold`); commit buttons in the trailing row, 8px apart. No dimmed backdrop.
- **Describe field:** label "Describe your topic" (`BodySemibold`, associated with the input per §4.4 naming) + multi-line §4.4 input (min 64px, top-aligned), placeholder "e.g. Common SQL injection attacks and how to prevent them" (`TextTertiary`). Required 5–300 chars (FR1); violation → §4.4 inline error line directly below, `Caption` `DangerText`: "Use between 5 and 300 characters."
- **Generate row:** "Generate with AI" primary (§4.1), 12px below the field, leading-aligned; the quiet note "Needs an internet connection." (`Caption` `TextSecondary`) 8px below the button — §5.11's voice.
- **Busy state:** the Generate button enters its disabled state with the label **"Generating…"**; the describe field disables (§4.4 disabled recipe); the **§4.13 busy block appears in place of the note**: marquee track (240px) + "Generating topic…" (`Caption`) + live elapsed-seconds counter + a "Cancel" secondary beside the track — the app's one busy pattern, reused verbatim. The call runs off the UI thread (R-5 #3; a frozen dialog is a review defect). Cancel follows §5.11's semantics: request aborted, or the late result discarded — never a half-saved topic. **Save is disabled while busy.** Preview fields stay enabled (manual editing is always possible, FR1).
- **Preview group (on success):** quiet group header "AI suggestion — edit anything before saving" (`Caption` `TextSecondary`); then **Name** (§4.4 input), **Category** (§4.5 combo — exactly `Programming` / `Cybersecurity`, `DropDownStyle = DropDownList`, never free text, R-2 #7), **Description** (multi-line §4.4) — all prefilled from the AI result, all editable, 16px field↔field. Manual-first: the dialog opens with these fields empty and enabled; filling them and pressing Save without generating is a first-class path.
- **Typed failure line (generation class):** a compact banner between the Generate row and the preview group (8px gaps): `WarningTint` wash, 8px radius, 12px padding — line 1 (`BodySemibold`, `WarningText`): "Topic generation is unavailable right now."; line 2 (`Body`, `TextPrimary`): the typed reason, **verbatim from §5.11's map** ("No AI provider is configured." / "The AI provider rejected the request. Check the provider configuration." / "The AI provider reached its request limit. Try again later." / "Couldn't reach the AI provider. Check the internet connection."); line 3 (`Body`, `TextPrimary`): "You can still fill in the fields manually and save." — the §5.11 offline-pointer pattern. The banner persists until a successful generate or dialog close. Rationale: generation unavailability is the *warning* class (§5.11), not the validation class — field-validation errors stay the §4.4 inline `DangerText` lines. Two failure classes, two placements, never mixed.
- **Commit row:** **Save** (primary, trailing, default) / **Cancel** (secondary, never the default — §4.11). Save runs the existing duplicate/empty validation (FR1): "Enter a topic name." / "A topic with this name already exists." (R-2 #5) as inline errors. Success closes the dialog; "Topic saved." lands in the host screen's strip (§5.3 feedback unchanged).
- **Accessibility:** labels associated per §4.4; the banner exposes `StaticText` with its text; the busy state is announced by the button's accessible name becoming "Generating…"; the marquee runs only while real work runs (§4.13 stillness rule).

### §5.7a Result reveal (amends §5.7's hero card; carries A2)

- **The hero card gains, below the score/context lines:** 16px; **XP line**: "+120 XP" (`BodySemibold`, `AccentText` — 7.18 ✓). 8px; **level row**: "Lvl 4" chip (§4.14 recipe) + the 8px progress track (`Fill` track, `Accent` fill) filling the remaining width + "70 XP to Lvl 5" (`Caption` `TextSecondary`) right-aligned. 16px; **achievement chips** (only when the submit unlocked something): quiet group label "Achievements unlocked" (`Caption` `TextSecondary`), 8px, then a wrapping chip row (8px gaps): each chip 28px tall, `RadiusChip` 4, 8px horizontal padding, `AccentTint` fill — glyph (MDL2 10 pt, `AccentText`, 5.83 on the tint ✓) + 4px gap + title (`CaptionSemibold` `TextPrimary`). Only **newly unlocked** achievements appear; if none, the block is omitted entirely (omission, not a "none" message — restraint).
- **Values shown are post-submit** (XP/level derived from the stored attempt per A3; the UI reads `ILevelService`/evaluator output — no XP arithmetic in the painter).
- **Motion restraint (Lead decision: no fade ships):** the reveal is an instant paint change — no confetti, no glow, no scale/bounce, no fade, no looping anything. All other §5.7 anatomy and states are unchanged; the reveal adds no states (the attempt always exists by construction).

---

## 4. §2.6 amendment — Accessibility & states on dark

- **Focus is visible (re-issued ring rule — amends the §2.6 focus bullet and the §4.1/§4.3/§4.4/§4.6/§4.9 state tables):** exactly one ring vocabulary, 1px, inset 1px, never a glow:
  1. **Default ring = `AccentText`** — on `Background` 7.72, `Surface` 7.18, `Fill` 6.08, `AccentTint` 5.83, `NavItemPressed` 6.20 — all ≥3.0. Applies to secondary buttons, nav items, segmented controls, grid action cells, and the **input focus border swap** (§4.4's border swap becomes `AccentText`).
  2. **Ring over filled `Accent`/`Danger` fills = `TextPrimary`** — 4.04 / 4.15 ✓ (the light rule "Accent outline on Accent fill" is invisible on dark: 2.0:1 — this case was latent in light too; fixed here).
  3. **Ring over `AccentTint` selection = `AccentText`** (5.83 ✓; plain `Accent` measures 2.91 — documented fail).
  The `AnswerTile`'s 2px white focus ring is unchanged (≥4.82 on every tile and wash).
- **Disabled states:** one recipe — dim variant fill (`AccentDisabled` `#262938` / `DangerDisabled` `#2E2325` / `Fill` for inputs) + `TextTertiary` label/content, no border on inputs. All EXEMPT but kept perceivable (3.03–3.19). Rationale recorded: Plan's `#3F3B5E`+white-label disabled measured 14.42:1 and read as enabled. Disabled nav items during generation (§5.2) use `TextTertiary` glyph + label on transparent.
- **AA-exempt roles:** `TextTertiary` is reserved for placeholders, disabled content, and decorative hints — **nothing a user must read** (validation errors, banners, empty-state explanations, unlock rules) may use it; those use `TextPrimary` or the semantic *text* tokens. Placeholder contrast (3.66 on Surface) is reported but exempt.
- **No color-only meaning (carried):** Draft/Approved badges keep labels; correct/incorrect keep glyph + word; the signed-in row keeps weight + "You"; difficulty chips are text-on-wash; the reveal states XP numerically.
- **Target sizes and keyboard contracts (unchanged):** rows ≥36px, buttons ≥32px, full-width nav hit areas; §4.6's grid keyboard contract applies to every action grid; Tab/arrows remain first-class.
- **Reduced motion:** the app's single animated element remains the §4.13 marquee (runs only while real work runs). No other motion exists to reduce (the §5.7a reveal ships without a fade, per Lead decision).

---

## 5. WP3 Painter audit checklist (per-hazard, with the dark replacement rule)

Run this against **every** `src/CodeQuest/UI/*.cs` file (28 files: `AnswerTile, AttemptReviewForm, CardPanel, DialogForm, FlatButton, GenerateQuizForm, HistoryForm, IosListGrid, IModulePanel, InputArea, InputField, LiveStatusLabel, LoginForm, MainForm, MarkedCheckBox, NavItemButton, ProgressTrack, QuizCard, QuizListFilter, QuizTakeForm, ReportsForm, SegmentedControl, StatusBadge, ThemedComboBox, TopicManageForm, UiFormat, UpdateDialog, UITheme`).

1. **Corner-cutout fill assumptions (`BackColor` ≠ actual parent surface).** Custom controls pre-fill their client rect with a hardcoded "parent" color before painting rounded paths; on dark, the wrong value shows as square-corner artifacts with real contrast. Known instances: `FlatButton.cs:44` (Surface — breaks on `SurfaceRaised` dialogs), `MarkedCheckBox.cs:41`, `InputField.cs:47/54/64`, `InputArea.cs:42/51/61`, `ProgressTrack.cs:32` (Background — breaks when a track sits on a card, e.g. §5.7a), `DialogForm.cs:58/204/216` (**must become `SurfaceRaised`**), `AnswerTile.cs:43` (tile color corners), `LoginForm.cs:72–95` (the "CQ" brand chip fills corners with `Accent` — on `#101216` that artifact is high-contrast; clip with a `Region` like `CardPanel` or fill corners with the true parent). **Rule:** the corner fill must equal the control's *actual* container surface — add an explicit `ParentSurface` property or resolve the parent's `BackColor`; never assume.
2. **`DialogForm`:** swap `BackColor` → `SurfaceRaised` (label surfaces at lines 204/216 too), border pen `SeparatorHairline` → `BorderHairline` (line 233), add `CS_DROPSHADOW` via `CreateParams` (0x20000; `DropShadow` class style). Mark circles (`DangerTint`/`WarningTint` + `DangerText`/`WarningText` glyphs, lines 46–48) are validated on dark (5.77/6.70) — no change; glyphs `E783`/`E7BA` verified.
3. **Grid column headers (`IosListGrid.cs:141`):** `ForeColor = UITheme.Accent` → **`AccentText`** (Accent on dark Surface = 3.59, fails AA for `Caption` 9pt headers; `AccentText` = 7.18). Also set **`DataGridView.BackgroundColor`** explicitly to `Surface` (it defaults to a light system color and will render a light band around the grid on dark), and confirm `EnableHeadersVisualStyles = false` stays (line 134 ✓), `SelectionBackColor = AccentTint` (line 158 ✓ token-driven), header `SelectionBackColor` (line 145) → keep token-driven. The `RowPostPaint` separator pen must read `SeparatorHairline` (token re-issue covers it).
4. **`ControlPaint.*` — the one live instance is `UITheme.TileHover` (`ControlPaint.Dark(tile, 0.08f)`).** On the re-issued dark tiles the delta is imperceptible and `ControlPaint` math assumes light UI. Replace with explicit per-tile hover constants = fill blended **15% toward black** (dark theme interacts by darkening vivid fills; white labels rise to ≥6.2 — never lighten, it breaks 4.5). Sweep for any other `ControlPaint.Light/Dark` creep — banned.
5. **White-on-fill pills (`HistoryForm.cs:186–190 + 235–246`):** the score pill paints `TextOnAccent` on `LevelEasy/Medium/Hard` fills — on dark that is **2.26 / 2.10 / 2.72:1** (and light's 4.17 already failed). Fix: pill = wash fill (`SuccessTint`/`WarningTint`/`DangerTint`) + `LevelEasy/Medium/Hard` text (6.30/6.70/5.77 ✓). Apply the same recipe in both themes (single source). Grep all `Level*`/`Tile*` used as `BackColor` behind white text — on dark only *non-text* uses of those fills are legal (bar fills, marks, tile fills with their own validated white labels).
6. **Hard-coded light literals:** none found (`Color.White/Black`, hex literals) — keep it that way; the only alpha literal is `AnswerTile.cs:130` `FromArgb(52, TextOnAccent)` (badge circle) — **validate visually on dark, don't token-ize**; it works on both.
7. **`SystemColors.*`:** none found in `UI/` — the audit's job is to keep it that way; any new painter must read `UITheme` only (spec §8). Watch for indirect system colors: `DataGridView.BackgroundColor`, `Color.Empty` fills, `Parent.BackColor` assumptions.
8. **Native chrome (verify on dark, accept as platform chrome per the recorded §4.6 scrollbar tradeoff):** `ThemedComboBox` closed face is owner-drawn (DropDownList + OwnerDrawFixed draws it via `OnDrawItem`) ✓, but the **drop arrow, dropdown border, and dropdown scrollbar** stay native — verify they don't render white-on-white; `FlatButton`/`NavItemButton` already set `UseVisualStyleBackColor = false` ✓ (keep — the themed default face would bleed through the rounded corners); no `DateTimePicker`/native `CheckBox`/`RadioButton`/`NumericUpDown` chrome may appear (the custom implementations own those recipes).
9. **Label parent-mismatch on tinted/washed containers:** every `Label.BackColor` must equal its container (`AccentTint` rows, wash banners, `Fill` inset boxes) or the label shows as a box; sweep `GenerateQuizForm` banners (token-driven `WarningTint` — validated 8.29/6.70 ✓), `AttemptReviewForm` explanation insets (`Fill` — `TextPrimary` 12.27 / `TextSecondary` 5.83 ✓), `TopicManageForm` (WP4 reworks it anyway).
10. **Focus rings:** `FlatButton.cs:135` paints the focus pen in `Accent` — on filled variants that is invisible on dark (2.0:1); implement §4's three-case ring rule (filled → `TextPrimary`; everything else → `AccentText`). Same check in `NavItemButton`, `SegmentedControl`, `IosListGrid` action cells, `MarkedCheckBox`.
11. **Token discipline:** re-issue values in `UITheme.cs` only — zero hex values in forms; correct the stale tile-contrast comments (§1.5) in the same pass; new tokens to add: `SurfaceRaised`, `BorderHairline`, `AccentText`, `SegmentHover`, `DangerHover/Pressed/Disabled`, `AchievementGlyph` font, per-tile dark hover constants.

---

## Sections this amendment supersedes or amends

- **§2 preamble** — "Dark mode is out of scope… one light appearance": **superseded** (owner directive: dark-only).
- **§2.1** — all value tables + both 2026-11-08 fun amendments: **re-issued** (names/structure kept; values per §1; `AccentText`/`SurfaceRaised`/`BorderHairline`/`SegmentHover`/danger states added; the "light neutral surfaces" framing replaced).
- **§2.6** — focus bullet (ring colors), contrast bullet (dark values; `TextOnAccent` rule extended to tiles), the D3-era light numbers.
- **§4.1** — secondary label ink (`AccentText`), disabled label/fill rule, focus ring on filled variants. **§4.2** — cards gain the 1px `BorderHairline` (dark). **§4.3** — active glyph ink, pressed value. **§4.4/§4.5** — focus border swap → `AccentText`; disabled values. **§4.6** — header ink; `DataGridView.BackgroundColor` rule. **§4.9** — hover token. **§4.11** — `SurfaceRaised` + `BorderHairline` + `CS_DROPSHADOW` (sign 8 of **§6** amended: the dialog is the single native-shadow exception). **§4.13** — unchanged; progress-fill margin noted (3.04).
- **§5 preamble** — status-strip verdicts use the semantic text tokens on dark (F3 interim rule definitively discarded). **§5.2** — nav entries + account block (→ §4.14/§5.2a). **§5.3** — topic form superseded by **§5.3a** (edit flow unchanged). **§5.6/§5.9** — wash/verdict values re-validated; tile hover mechanism changed (§2). **§5.7** — hero card gains **§5.7a**. New: **§5.13, §5.14**.
- **§6** — sign 1's stale "#0088FF blue" note refreshed (accent is indigo); sign 7 clarified (reveal ships still — no fade); sign 8 amended as above.
- **`UITheme.cs`** — re-issue + comment corrections (WP3); **Plan §4** — superseded by this amendment's §1 values (validated/adjusted).
