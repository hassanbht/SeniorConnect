# ADR-022 — State Management Migration from SetState to Riverpod 2 (Notifier / StateNotifier & Sealed AsyncState)

Status: Accepted  
Date: 2026-09  
Amends: ADR-001 (Modular Monolith / Mobile Architecture)

---

## 1. Context

Across the Flutter mobile application (`mobile/senior_connect`), 34 screens and widgets were previously managing asynchronous operations, API calls, error handling, and form states directly within widget lifecycles using `setState`. 

This violated several binding principles outlined in `AGENTS.md` and `mitanand-flutter-ux`:
1. **Separation of Concerns:** Business logic, API calls, and domain computations occurred inside widget state trees (`State<MyWidget>`).
2. **State Lifecycle Uniformity:** Screens lacked a uniform 5-state lifecycle (`initial`, `loading`, `loaded`, `empty`, `error`), occasionally resulting in inconsistent empty or error presentations.
3. **Accessibility & Testability:** States tied to widget instances could not easily be unit-tested or observed without rendering the entire tree.

The architecture required an idiomatic, lightweight, and compile-time safe state management framework that did not require code-generation (`build_runner` or `freezed`) overhead.

---

## 2. Decision

### 2.1 Adoption of Riverpod 2 (`flutter_riverpod`)
We integrate `flutter_riverpod: ^2.6.1` as the client-side state management library.
- The application root (`SeniorConnectApp` in `lib/main.dart`) is enclosed in a root `ProviderScope`.
- The testing matrix harness (`wrapForTest` in `test/matrix.dart`) is enclosed in `ProviderScope` to ensure seamless automated matrix testing across brightness, locales, and text scaling factors.

### 2.2 Native Dart 3 Sealed Hierarchy: `AsyncState<T>`
Rather than relying on code-generated union types, we implement an immutable, zero-dependency Dart 3 sealed class `AsyncState<T>` located in `lib/shared/riverpod/async_state.dart`:
- `AsyncInitial<T>`: Uninitialized state.
- `AsyncLoading<T>`: Async operation in flight.
- `AsyncLoaded<T>(T data)`: Successfully loaded payload.
- `AsyncEmpty<T>([String? message])`: Explicit empty state (e.g., zero search results or empty activity queues).
- `AsyncError<T>(String message, [Object? error, StackTrace? stackTrace])`: Failure state carrying human-readable error keys and optional debug information.

Exhaustive pattern matching is provided via `.when(...)` and `.maybeWhen(...)`, enforcing compile-time handling of all 5 states in widget builds.

### 2.3 Layered Application Notifiers
Every feature contains an `application/` layer housing dedicated `StateNotifier` / `Notifier` subclasses and corresponding family providers:
- **Profile:** `ProfileViewNotifier`, `ProfileEditNotifier`, `HelpFaqNotifier`
- **Auth:** `AuthNotifier`, `OtpNotifier`, `DeviceListNotifier`, `EmailLinkNotifier`, `StaffLoginNotifier`, `TotpEnrollmentNotifier`
- **Help Requests:** `VolunteerFeedNotifier`, `MyRequestStatusNotifier`, `CreateHelpRequestNotifier`, `ActiveAssignmentNotifier`, `SeniorRequestFlowNotifier`, `VoiceRequestNotifier`, `SafeguardingConcernNotifier`
- **Organizations:** `OrganizationsListNotifier`, `OrganizationProfileNotifier`, `OrgPostFormNotifier`, `LogActivityNotifier`, `CoordinatorAttentionNotifier`, `CoordinatorBulkEntryNotifier`, `CoordinatorHoursQueueNotifier`, `CoordinatorRosterNotifier`, `IntakeFormNotifier`, `IntakeFormSubmissionsNotifier`
- **Family:** `SeniorAccessLogNotifier`, `FamilyDashboardNotifier`, `DelegationPermissionsNotifier`
- **Community:** `CommunityFeedNotifier`, `EventDetailNotifier`, `MyAppointmentsNotifier`
- **Discovery:** `DiscoveryNotifier`

### 2.4 Sole Permitted Exception
`shared/widgets/app_button.dart` internal visual busy toggle (`_busy`) is the **only** permitted `setState` in the entire codebase. It is a strictly local visual debounce/animation toggle containing zero business logic. All other 33 widgets are migrated to `ConsumerWidget` or `ConsumerStatefulWidget` (where controllers or animation mixins require explicit disposal).

---

## 3. Consequences

### Positive
- **Zero Business Logic in Widgets:** Screens purely consume state (`ref.watch`) and dispatch user intentions (`ref.read(...notifier).method()`).
- **Exhaustive UI Handling:** Every screen cleanly presents loading, empty, error, and loaded states with retry capabilities.
- **Pure Dart 3 without Code Generation:** No build_runner or freezed generator required; instantaneous compile and test cycle.
- **Architectural Purity:** Clean separation between presentation (`ConsumerWidget`), application (`Notifier`), data (`Repository` / `ApiClient`), and domain models.

### Neutral / Maintenance
- Any new widget test using matrix or standalone rendering must execute inside a `ProviderScope` (automatically provided when using `wrapForTest`).
