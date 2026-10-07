# Accessibility

> Accessibility here is not compliance theatre. The primary user has reduced vision,
> reduced fine motor control, and low technology confidence. An inaccessible screen is an
> unusable product, not a partially usable one.

## 1. Standard we hold ourselves to

**WCAG 2.2 Level AA as the floor, AAA for text contrast**, plus the mobile-specific
criteria of EN 301 549.

On the legal question: Austria's Barrierefreiheitsgesetz (in force since 28 June 2025)
covers specific categories of products and services. A social/charitable app is not
automatically in scope simply because its users are older or disabled, and there are
exemptions for microenterprises. **Do not claim legal obligation in a proposal without
checking with a lawyer** — but do build to the standard anyway, because for this audience
it is a product-quality requirement, and because organizations will ask.

## 2. Per-screen requirements

```
[ ] Contrast: body text >= 7:1, large text and UI components >= 4.5:1
[ ] Touch targets >= 48dp (standard) / >= 64dp (Senior Mode), 12dp apart
[ ] Layout intact at text scale 1.0 / 1.5 / 2.0
[ ] Every interactive element has a semantic label describing the ACTION
[ ] Focus order is logical; nothing is focusable-but-invisible
[ ] Colour is never the only carrier of meaning
[ ] Errors: announced to the screen reader, in words, with what to do next
[ ] No time limits; no auto-advancing content; no auto-playing media
[ ] Reduce Motion respected
[ ] Works in LTR and RTL
[ ] Works in light and dark
```

## 3. Flutter specifics

```dart
// Label the action, not the widget
Semantics(
  button: true,
  label: 'hilfe_anfragen.semantic'.tr(),   // "Hilfe anfragen. Öffnet das Formular."
  child: AppButton(...),
)

// Group related content so the screen reader reads it as one item
MergeSemantics(child: HelpRequestCard(...))

// Hide purely decorative elements
ExcludeSemantics(child: DecorativeIllustration())

// Announce async results — a silent success is a failure for a screen-reader user
SemanticsService.announce('anfrage_gesendet'.tr(), TextDirection.ltr);

// Never do this:
// textScaler: TextScaler.noScaling   ← forbidden, treated as a build-breaking defect
```

Live regions for anything that changes without user action (a match found, a status
change) so screen readers announce it.

## 4. Senior Mode specification

| Property | Standard | Senior Mode |
| --- | --- | --- |
| Base body size | 17 | 21 |
| Minimum touch target | 48 dp | 64 dp |
| Primary button height | 52 dp | 72 dp |
| Actions on the home screen | unlimited | **max 5** |
| Navigation | bottom nav (up to 5 tabs) | full-width action list, no tabs |
| Screens between home and a completed help request | ≤ 6 | **≤ 5 taps total** |
| Density | comfortable | spacious |
| Gestures | swipe allowed as a shortcut | **no gesture-only actions** |
| Confirmation | for destructive actions | for **every** consequential action |
| Icons | may stand alone | **always with a text label** |

Senior Mode is a device preference, not a role. Anyone can enable it. It should be offered
during onboarding based on a plain question, not on the user's age:

> „Möchten Sie größere Schrift und größere Schaltflächen?"  [ Ja ] [ Nein ]

Never label it "Seniorenmodus" in the UI. Call it **„Große Ansicht"**. Nobody wants to
select the mode that identifies them as old.

## 5. Language

- Target **B1 German**, tending toward *Leichte Sprache* on critical paths.
- One idea per sentence. Active voice. Verbs, not nominalisations.
- Never: „Synchronisierung fehlgeschlagen", „Token abgelaufen", „Profil aktualisiert".
- Always: „Das hat nicht geklappt. Bitte versuchen Sie es nochmal."
- Every error names the next action.
- Numbers written as digits. Dates written out: „Dienstag, 25. August".

## 6. Testing protocol (each phase, manually)

```
1. TalkBack (Android) — complete the primary journey with the screen off
2. VoiceOver (iOS) — same
3. System text size at maximum — every screen
4. Display size / zoom at maximum — every screen
5. Grayscale mode — is anything now ambiguous?
6. One-handed, thumb only, standing up
7. Reduce Motion on
8. Dark mode + Senior Mode + 200 % text, together
9. Give the phone to someone over 70 and say nothing. Watch. Write down every hesitation.
```

Step 9 is the only one that finds the real problems. Do it every phase.

## 7. Automated checks in CI

```
flutter test --tags accessibility     # golden tests at 1.0 / 1.5 / 2.0
accessibility_guideline tests: androidTapTargetGuideline, iOSTapTargetGuideline,
                               labeledTapTargetGuideline, textContrastGuideline
custom lint: no hardcoded Colors.*
custom lint: no raw user-visible string literals in widgets
```
