# Flutter Development Rules

Read the root `AGENTS.md` first. These rules are additional.

## Architecture

Feature-first. Each feature contains:

```
features/<name>/
  data/          dtos, remote data source, repository implementation
  domain/        entities, repository interfaces
  application/   Riverpod StateNotifier/Notifier + sealed AsyncState (ADR-022)
  presentation/  pages, widgets
```

No global god-service. No business logic in widgets. No direct HTTP from a widget.

Flutter is **never** the source of truth for: authorization, trust level, safety level,
matching eligibility, or what a user is allowed to do. The backend decides; the client
renders the decision.

---

## Design system

Everything visual comes from `core/design_system/`:

```
AppColors        light + dark + high-contrast token sets
AppTypography    standard + senior scales
AppSpacing       4pt grid
AppRadius
AppBreakpoints
AppTheme         builds ThemeData for each AppThemeMode
```

```
✗ Colors.white, Colors.black, Color(0xFF…) anywhere in features/
✗ Hardcoded EdgeInsets numbers — use AppSpacing
✗ Hardcoded font sizes — use Theme.of(context).textTheme
✓ Theme.of(context).colorScheme.* and context.appColors.* only
```

## Dark mode

Dark mode ships in Phase 1 and is verified on every screen, every phase.

```
[ ] No hardcoded colours
[ ] Images have a dark variant or a transparent background
[ ] Elevation via surfaceElevated colour, not shadow
[ ] Status colours still pass 4.5:1 on the dark surface
[ ] Screenshots in both modes attached to the PR
```

## Localization

```
easy_localization · assets/translations/{de,en,fa}.json
German is the source of truth. Author every key in de.json first.
```

```
✗ Any user-visible string literal in a Dart file
✗ Translated text used as a business value — if (status == "Abgeschlossen") is a defect
✓ Stable enums and codes for statuses, roles, categories, safety levels
✓ Translate only at the presentation layer
```

Key structure, grouped by feature:

```json
{
  "common":  { "save": "Speichern", "cancel": "Abbrechen" },
  "help":    { "create": { "title": "Hilfe anfragen" } }
}
```

Adding a key means adding it to **all four** locale files in the same change. A missing
key is a build-breaking defect.

## RTL

Persian is supported, so:

```
✗ EdgeInsets.only(left: …)          ✓ EdgeInsetsDirectional.only(start: …)
✗ Alignment.centerLeft              ✓ AlignmentDirectional.centerStart
✗ Row with hardcoded left/right assumptions
✓ Directional icons mirror; non-directional icons do not
```

Verify every new screen in `fa` before marking it done.

## Responsive

Use `flutter_screenutil` **where it helps**, not everywhere. Do not blindly convert every
dimension to `.w`, `.h`, `.sp`.

```
✓ LayoutBuilder, Flexible, Expanded, Wrap, MediaQuery
✓ AppBreakpoints: compact <600 · medium 600-904 · expanded 905-1279 · large >=1280
✗ Fixed-width layouts
✗ .sp on typography — typography must follow the OS text scale, not the design width
```

Senior Mode is single-column at every breakpoint. Do not build a two-pane tablet layout
for seniors.

## Accessibility

```
✗ textScaler: TextScaler.noScaling            ← build-breaking defect
✗ Icon-only interactive elements in senior-facing screens
✗ Gesture-only actions (swipe to delete, long-press only)
✗ Time limits, countdowns, auto-advancing content
✓ Semantics label describing the ACTION on every interactive element
✓ 48dp minimum touch target (64dp in Senior Mode), 12dp apart
✓ Layout verified at text scale 1.0 / 1.5 / 2.0
✓ SemanticsService.announce for async results
✓ MediaQuery.disableAnimations respected
```

Accessibility regressions are bugs, not backlog items.

## Senior Mode

A device preference, not a role. Labelled **„Große Ansicht"** in the UI, never
„Seniorenmodus". Changes: type scale, touch targets, navigation shell, information density,
confirmation behaviour. Maximum 5 actions on the home screen.

## State

```
Cubit + Freezed. One cubit per feature area, not one per app.
States: initial / loading / loaded / empty / error — all five, every time.
No unrelated business state in a single global store.
```

## Every screen must have

```
[ ] Loading state
[ ] Empty state (designed, with an action — "Keine Daten" is a bug)
[ ] Error state with a retry and a human-readable message
[ ] Offline behaviour defined (from Phase 7)
[ ] Senior Mode variant considered
[ ] All three locales
[ ] Light + dark
[ ] Text scale 1.0 / 1.5 / 2.0
```

## New screen checklist (5 steps, none optional)

```
1. Page + widgets in features/<name>/presentation/
2. Cubit + Freezed state in features/<name>/application/
3. DI registration
4. Route in go_router with the correct guard
5. Translation keys in ALL FOUR locale files
```
