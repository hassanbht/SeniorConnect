# SeniorConnect Design System

> This document is the single source of truth for colour, type, spacing and component
> behaviour. Dart implementations live in `starter/flutter/`. If the code and this document
> disagree, this document wins and the code is a bug.

---

## 1. Design principles

1. **Legibility beats beauty.** Every decision is validated at 200 % text scale.
2. **Warm, not clinical.** Warm neutrals, never blue-grey. This is a neighbourhood, not a hospital.
3. **One primary action per screen.** Seniors abandon screens with competing calls to action.
4. **Colour is never the only signal.** Every state also has an icon and a text label.
5. **No hidden gestures.** No swipe-to-delete, no long-press-only actions, no bottom-sheet-only navigation for senior-facing screens.
6. **Dark mode is a first-class theme**, not an inverted afterthought. It ships in Phase 1.

---

## 2. Colour

### 2.1 Rationale

- **Primary — deep petrol green (`#14625A`).** Reads as trustworthy and calm without being
  "medical blue" (which every care product uses and which older users associate with
  hospitals and insurance forms). Green also carries "growth / life / outdoors" which fits
  the community half of the product.
- **Accent — warm amber (`#B36A00` family).** Complements the green, signals warmth and
  human activity. Used for secondary actions and highlights, **never** for destructive or
  emergency states.
- **Neutrals are warm** (a hint of yellow/brown in the greys). Cold greys make interfaces
  feel institutional.
- **Red is reserved.** `#C0261F` appears *only* for Emergency / SOS. Ordinary validation
  errors use the softer `#B3261E` error token so that the emergency red keeps its meaning.

### 2.2 Light theme tokens

| Token | Hex | Use | Contrast vs. its background |
| --- | --- | --- | --- |
| `background` | `#FAF8F5` | app scaffold | — |
| `surface` | `#FFFFFF` | cards, sheets | — |
| `surfaceVariant` | `#F0EDE8` | subtle fills, chips, disabled fields | — |
| `surfaceElevated` | `#FFFFFF` + shadow | dialogs, menus | — |
| `onSurface` | `#1B1A18` | primary text | **16.4 : 1** (AAA) |
| `onSurfaceVariant` | `#4A453E` | secondary text, labels | **9.0 : 1** (AAA) |
| `outline` | `#857F76` | borders on interactive elements | 3.7 : 1 (AA for UI components) |
| `outlineVariant` | `#D5CFC6` | dividers, decorative only | 1.5 : 1 — **never used for text or focus** |
| `primary` | `#14625A` | primary buttons, active states, links | **6.8 : 1** (AA, AAA-large) |
| `onPrimary` | `#FFFFFF` | text on primary | **7.2 : 1** (AAA) |
| `primaryContainer` | `#A8F2E4` | selected chips, highlights | — |
| `onPrimaryContainer` | `#00201C` | text on primaryContainer | **13.5 : 1** (AAA) |
| `accent` | `#B36A00` | secondary buttons, badges | — |
| `accentText` | `#8A5100` | accent-coloured text | **6.1 : 1** (AA) |
| `accentContainer` | `#FFDDB5` | warning banners | — |
| `onAccentContainer` | `#2C1600` | text on accentContainer | AAA |
| `success` | `#2E6B3A` | completed, verified | **6.0 : 1** (AA) |
| `successContainer` | `#C6EFCB` | success banner fill | — |
| `error` | `#B3261E` | validation errors | **6.2 : 1** (AA) |
| `onError` | `#FFFFFF` | text on error | **6.5 : 1** (AA) |
| `errorContainer` | `#F9DEDC` | error banner fill | — |
| `onErrorContainer` | `#410E0B` | text on errorContainer | AAA |
| `emergency` | `#C0261F` | **SOS / Notruf only** | **5.9 : 1** on white |
| `scrim` | `#00000066` | modal backdrop | — |

### 2.3 Dark theme tokens

Dark mode uses a **warm near-black**, not pure black. Pure black on OLED creates
uncomfortable halation around light text for readers with cataracts or astigmatism —
which is a large share of the target group.

| Token | Hex | Contrast |
| --- | --- | --- |
| `background` | `#161513` | — |
| `surface` | `#1E1D1A` | — |
| `surfaceVariant` | `#2A2825` | — |
| `surfaceElevated` | `#252320` | — |
| `onSurface` | `#F2EEE8` | **14.6 : 1** (AAA) |
| `onSurfaceVariant` | `#CFC8BF` | **10.2 : 1** (AAA) |
| `outline` | `#9A938A` | **5.6 : 1** |
| `outlineVariant` | `#3A3733` | decorative only |
| `primary` | `#6FD9C8` | **10.0 : 1** (AAA) |
| `onPrimary` | `#00382F` | **7.7 : 1** (AAA) |
| `primaryContainer` | `#00504A` | — |
| `onPrimaryContainer` | `#A8F2E4` | **7.3 : 1** (AAA) |
| `accent` | `#FFB870` | **9.9 : 1** (AAA) |
| `onAccent` | `#4A2800` | — |
| `accentContainer` | `#6B3F00` | — |
| `onAccentContainer` | `#FFDDB5` | — |
| `success` | `#7FD68C` | **9.6 : 1** (AAA) |
| `error` | `#FFB4AB` | **9.9 : 1** (AAA) |
| `onError` | `#690005` | — |
| `errorContainer` | `#93000A` | — |
| `onErrorContainer` | `#FFDAD6` | — |
| `emergency` | `#FF6B60` | high-visibility red on dark |
| `scrim` | `#000000A6` | — |

### 2.4 High-contrast variants (Phase 5)

Two extra themes selectable in Settings → Darstellung:

- **Hoher Kontrast (hell)** — `onSurface` → `#000000`, `background` → `#FFFFFF`,
  all outlines → `#000000` at 2 px, no shadows, no `surfaceVariant` fills.
- **Hoher Kontrast (dunkel)** — `onSurface` → `#FFFFFF`, `background` → `#000000`,
  outlines → `#FFFFFF`.

These are separate `ThemeData` objects, not filters. Build them in Phase 5 but reserve
the `AppThemeMode` enum slots in Phase 1 so no refactor is needed.

### 2.5 Theme mode

```dart
enum AppThemeMode { system, light, dark, highContrastLight, highContrastDark }
```

- Default: `system`.
- Persisted per device in secure/shared storage, **not** on the server (it is a device
  preference, not a user preference — a senior's tablet and their daughter's phone
  legitimately differ).
- Senior Mode is **orthogonal** to theme mode. A user can be in Senior Mode + dark.

---

## 3. Typography

### 3.1 Typefaces

| Script | Font | Why |
| --- | --- | --- |
| Latin (de, en) | **Atkinson Hyperlegible Next** | Designed by the Braille Institute specifically for low vision. Disambiguates I/l/1 and O/0. Free, OFL licensed. |
| Arabic / Persian (fa, ar) | **Vazirmatn** | Excellent Persian/Arabic coverage, free, harmonises in weight with Atkinson. |
| Fallback | **Inter** | If Atkinson is unavailable for a glyph. |

Bundle the fonts as assets. **Do not** load fonts from a network at runtime — it breaks
offline use and creates a GDPR problem (Google Fonts CDN requests transmit IP addresses;
this has already produced German court rulings).

### 3.2 Type scale

Two scales. `Standard` is for volunteers, families and staff. `Senior` is applied when
Senior Mode is on. Both scale further with the OS text-size setting — **never disable it**.

| Role | Standard | Senior | Weight | Line height |
| --- | --- | --- | --- | --- |
| `displayLarge` | 32 | 40 | 700 | 1.20 |
| `headlineLarge` | 26 | 32 | 700 | 1.25 |
| `headlineMedium` | 22 | 27 | 600 | 1.30 |
| `titleLarge` | 20 | 24 | 600 | 1.35 |
| `titleMedium` | 17 | 21 | 600 | 1.40 |
| `bodyLarge` | 17 | 21 | 400 | **1.55** |
| `bodyMedium` | 15 | 19 | 400 | **1.55** |
| `labelLarge` (buttons) | 17 | 21 | 600 | 1.20 |
| `labelSmall` | 13 | 16 | 500 | 1.30 |

Rules:

- **Minimum font size anywhere in the app: 13 sp.** No exceptions, including legal text,
  timestamps, and captions. If it does not fit at 13 sp, the layout is wrong.
- Body line height ≥ 1.5 — required by WCAG 1.4.12 and materially helps older readers.
- Maximum line length ~ 60–70 characters. On tablets, constrain text columns; do not let
  a paragraph span 1000 px.
- Never use font weight below 400. Light/thin weights are unusable for this audience.
- Never use all-caps for anything longer than a two-word button label.
- Never rely on italics to carry meaning.

### 3.3 Text scaling

```dart
// Clamp only the upper bound, and only to prevent total layout collapse.
MediaQuery.withClampedTextScaling(
  minScaleFactor: 1.0,
  maxScaleFactor: 2.0,
  child: child,
)
```

Every screen must be manually verified at scale factor **1.0, 1.5 and 2.0**. This is part
of the Definition of Done.

---

## 4. Spacing, radius, elevation

```dart
// 4pt base grid
xxs =  2   xs = 4    sm = 8    md = 12
lg  = 16   xl = 24   xxl = 32  xxxl = 48
```

| Radius | Value | Use |
| --- | --- | --- |
| `rSm` | 8 | chips, small inputs |
| `rMd` | 12 | text fields, list tiles |
| `rLg` | 16 | cards |
| `rXl` | 24 | bottom sheets, dialogs |
| `rFull` | 999 | avatars, pills |

Elevation: use **at most 3 levels**. Level 0 (flat), 1 (card, 2 dp), 2 (dialog, 8 dp).
In dark mode, express elevation with `surfaceElevated` colour, not with shadow.

---

## 5. Touch targets and layout

| Context | Minimum target |
| --- | --- |
| Standard mode | **48 × 48 dp** (WCAG 2.5.5 / Material) |
| Senior Mode | **64 × 64 dp** |
| Primary action button (Senior Mode) | full width, **72 dp** tall |
| Minimum gap between two tappable elements | 12 dp |

Senior Mode home screen: **maximum 5 actions**, one per row, full width, each with icon +
label. No tabs, no drawer, no carousel.

### Breakpoints

```dart
compact  <  600   // phone
medium   600–904  // large phone landscape / small tablet
expanded 905–1279 // tablet
large    >= 1280  // desktop web (staff dashboard)
```

Senior Mode is single-column at **every** breakpoint. Do not build a two-pane tablet
layout for seniors — it increases scanning cost with no benefit.

---

## 6. Iconography

- **Material Symbols Rounded**, weight 400, optical size 24/32/48.
- Icons in Senior Mode are 32 dp minimum, always paired with a text label.
- Never use an icon alone to convey a state or an action for a senior-facing element.
- Custom SVGs (if any) follow the single-path `fill-rule="evenodd"` convention for
  reliable `flutter_svg` rendering.

Semantic icon map (fixed — do not improvise per screen):

| Concept | Icon |
| --- | --- |
| Help request | `volunteer_activism` |
| Community group | `groups` |
| Event | `event` |
| Volunteer | `handshake` |
| Family | `family_restroom` |
| Verified | `verified_user` |
| Concern / safeguarding | `report` (restricted screens only) |
| Emergency | `emergency` |
| Voice input | `mic` |

---

## 7. Component rules

### Buttons

| Variant | Use |
| --- | --- |
| `FilledPrimary` | the one main action of the screen |
| `Tonal` | secondary actions |
| `Outlined` | tertiary / cancel |
| `Text` | inline links only, never as the main action |
| `Emergency` | red, only on the Emergency screen, requires a 2-step confirm |

Buttons in Senior Mode always show a **verb** ("Hilfe anfragen"), never a bare noun
("Anfrage") and never an icon alone.

### Status representation

Every status must render as **colour + icon + word**:

```
🟢 ✓  Bestätigt        (success)
🟡 ⏳ Wird gesucht      (accent)
🔵 ℹ  Geplant           (primary)
⚪ —  Abgeschlossen     (onSurfaceVariant)
🔴 ✕  Abgesagt          (error)
```

### Trust badges

Badges are **factual, never evaluative**. Never render a green tick meaning "safe".

```
Identität geprüft        — verified by whom, when
Telefon bestätigt
Adresse bestätigt
Von Caritas Tirol bestätigt
Schulung „Erste Hilfe" abgeschlossen
```

Tapping a badge opens a plain-language explanation of *what was checked and by whom* —
never the underlying document or personal data.

### Forms

- One question per screen for seniors.
- Labels above fields, never placeholder-only labels.
- Errors appear below the field, in words, with `error` colour **and** an icon.
- Never clear a form on error.
- Date and time pickers: large, wheel-style, with sensible defaults ("morgen", "nächste Woche").

### Empty states

Every list has a designed empty state with: an illustration or large icon, one sentence of
plain language, and one primary action. "Keine Daten" is a bug.

---

## 8. Motion

- Duration 150–250 ms, standard easing.
- **Respect `MediaQuery.disableAnimations`** (`Reduce Motion`) — when on, all transitions
  become instant cross-fades.
- No parallax, no auto-playing carousels, no bouncing attention-grabbers.
- Loading: a skeleton or a labelled spinner ("Wird geladen …"), never an unexplained spinner.

---

## 9. RTL

Persian and (later) Arabic are supported, so:

- `EdgeInsetsDirectional` / `AlignmentDirectional` everywhere — never `left`/`right`.
- Icons that imply direction (back, forward, send) must mirror; icons that do not
  (clock, phone, checkmark) must not.
- Numbers, times and phone numbers stay LTR inside RTL text.
- Verify every screen in `fa` before marking a feature done.

---

## 10. Dark mode checklist (per screen)

```
[ ] No hardcoded Colors.white / Colors.black anywhere
[ ] All colours come from Theme.of(context).colorScheme or AppColors extension
[ ] Images/illustrations have a dark variant or a transparent background
[ ] Logo uses the inverted mark
[ ] Elevation expressed via surfaceElevated, not shadow
[ ] Status colours still pass 4.5:1 on the dark surface
[ ] Screenshots taken in both modes and attached to the PR
```
