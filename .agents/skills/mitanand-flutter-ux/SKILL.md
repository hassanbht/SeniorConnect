---
name: SeniorConnect-flutter-ux
description: Use when implementing any Flutter screen, widget or theme change in SeniorConnect. Covers architecture, design system, dark mode, Senior Mode, localization, RTL, responsive layout and accessibility.
---

# SeniorConnect Flutter & UX

## Step 1 — Identify the actor

```
Senior · Family caregiver · Volunteer · Organization staff
```

Do **not** use the same UI complexity for all actors. A staff screen may have a data
table; a senior screen may not.

Ask: **is this screen reachable in Senior Mode?** If yes, the Senior Mode constraints below
are hard requirements, not suggestions.

## Step 2 — Define before building

```
User journey step (from docs/product/user-journeys.md)
Loading state
Empty state (designed, with an action)
Error state (human-readable, with a retry)
Offline behaviour (from Phase 7)
Accessibility requirements
Localization keys
```

## Step 3 — Build

Layer order: `data → domain → application (riverpod) → presentation`.

```
✗ Direct HTTP in a widget
✗ Business logic in a widget
✗ setState for anything beyond a purely local visual toggle
✓ riverpod + Freezed, five states: initial / loading / loaded / empty / error
```

## Design system — non-negotiable

```dart
// ✓
Theme.of(context).colorScheme.primary
context.appColors.success
Theme.of(context).textTheme.bodyLarge
AppSpacing.lg
AppRadius.card

// ✗  every one of these is a defect
Colors.white
Color(0xFF14625A)
const EdgeInsets.all(16)
TextStyle(fontSize: 16)
```

## Dark mode — verify every screen

```
[ ] No hardcoded colours
[ ] Image assets have a dark variant or a transparent background
[ ] Elevation via surfaceElevated, not shadow
[ ] Status and semantic colours still readable on the dark surface
[ ] Screenshot in both modes
```

## Senior Mode

```
Max 5 actions on the home screen, one per row, full width
Touch targets >= 64dp, 12dp apart
Primary button height 72dp
Icons always paired with a text label
No tabs, no drawer, no carousel, no gesture-only actions
Confirmation for every consequential action
Max 5 taps from home to a submitted help request
Single column at every breakpoint
```

Label it **„Große Ansicht"** in the UI, never „Seniorenmodus".

## Localization

```
✗ Any user-visible string literal in Dart
✗ if (status == "Abgeschlossen")   ← translated text as a business value
✓ Enums and codes for business values; .tr() only at the presentation layer
✓ Add every new key to de.json, en.json AND fa.json in the same change
✓ German is authored first and is the source of truth
```

## RTL

```
✓ EdgeInsetsDirectional / AlignmentDirectional everywhere
✓ Directional icons mirror; clocks, phones and checkmarks do not
✓ Numbers, times and phone numbers stay LTR inside RTL text
[ ] Verified in fa before marking done
```

## Responsive

```
✓ LayoutBuilder, Flexible, Expanded, Wrap, MediaQuery
✓ flutter_screenutil where it genuinely helps
✗ .sp for typography — type follows the OS text scale, not the design width
✗ Fixed-width layouts
✗ A layout that only works on one screen size
```

## Accessibility

```dart
Semantics(button: true, label: 'help.create.semantic'.tr(), child: AppButton(...))
MergeSemantics(child: HelpRequestCard(...))
ExcludeSemantics(child: DecorativeImage())
SemanticsService.announce('help.created.announcement'.tr(), TextDirection.ltr);
```

```
✗ textScaler: TextScaler.noScaling     ← treat as build-breaking
✗ Colour as the only carrier of meaning
✗ Placeholder-only form labels
✓ 48dp / 64dp touch targets
✓ Layout verified at text scale 1.0 / 1.5 / 2.0
✓ MediaQuery.disableAnimations respected
```

## New screen — the five steps

```
1. Page + widgets        features/<name>/presentation/
2. Cubit + Freezed state features/<name>/application/
3. DI registration
4. Route + guard         core/routing/
5. Translation keys      all three locale files
```

## Done checklist

```
[ ] Loading / empty / error states
[ ] de · en · fa
[ ] LTR + RTL
[ ] Light + dark
[ ] Text scale 1.0 / 1.5 / 2.0
[ ] Semantic labels, touch targets
[ ] Senior Mode variant if reachable
[ ] No hardcoded colours, sizes or strings
[ ] Screenshots attached
```
