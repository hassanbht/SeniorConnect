// lib/features/profile/application/profile_edit_notifier.dart
//
// Riverpod Notifier for ProfileEditScreen.
// All business logic is concentrated here — the widget is stateless.
// DO NOT use setState in the presentation layer. See AGENTS.md §State Management.

import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:image_picker/image_picker.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../data/geography_repository.dart';
import '../data/interests_repository.dart';
import '../data/profile_repository.dart';

part 'profile_edit_notifier.freezed.dart';
part 'profile_edit_notifier.g.dart';

@freezed
class ProfileEditState with _$ProfileEditState {
  const factory ProfileEditState({
    @Default(true) bool isLoadingProfile,
    @Default(false) bool isSaving,
    @Default(false) bool isVerifyingPhone,
    @Default(false) bool isGeocoding,
    @Default(false) bool isUploadingPhoto,
    @Default(false) bool phoneVerified,
    String? errorKey,
    double? latitude,
    double? longitude,
    @Default('de') String preferredLocale,
    @Default(false) bool seniorModeDefault,
    String? photoUrl,
    @Default([]) List<Bundesland> bundeslaender,
    @Default([]) List<Bezirk> bezirke,
    @Default([]) List<Gemeinde> gemeinden,
    String? selectedBundesland,
    String? selectedBezirk,
    String? selectedGemeinde,
    @Default([]) List<InterestOption> interestCatalog,
    @Default({}) Set<String> selectedInterestIds,
    @Default('') String initialName,
    @Default('') String initialEmail,
    @Default('') String initialPhone,
  }) = _ProfileEditState;
}

@riverpod
class ProfileEditNotifier extends _$ProfileEditNotifier {
  @override
  ProfileEditState build(
    ProfileRepository profileRepo,
    GeographyRepository geographyRepo,
    InterestsRepository interestsRepo,
  ) {
    Future(() => _initialize(profileRepo, geographyRepo, interestsRepo));
    return const ProfileEditState();
  }

  Future<void> _initialize(
    ProfileRepository profileRepo,
    GeographyRepository geographyRepo,
    InterestsRepository interestsRepo,
  ) async {
    await Future.wait([
      _loadCurrentUser(profileRepo),
      _loadBundeslaender(geographyRepo),
      _loadInterests(interestsRepo),
    ]);
    state = state.copyWith(isLoadingProfile: false);
  }

  Future<void> _loadCurrentUser(ProfileRepository profileRepo) async {
    try {
      final user = await profileRepo.getCurrentUser();
      state = state.copyWith(
        initialName: user.displayName,
        initialEmail: user.email ?? '',
        initialPhone: user.phone ?? '',
        preferredLocale: user.preferredLocale,
        seniorModeDefault: user.seniorModeDefault,
        photoUrl: user.photoUrl,
      );
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    }
  }

  Future<void> _loadBundeslaender(GeographyRepository geographyRepo) async {
    try {
      final list = await geographyRepo.getBundeslaender();
      state = state.copyWith(bundeslaender: list);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    }
  }

  Future<void> _loadInterests(InterestsRepository interestsRepo) async {
    try {
      final results = await Future.wait([
        interestsRepo.getCatalog(),
        interestsRepo.getSelected(),
      ]);
      state = state.copyWith(
        interestCatalog: results[0] as List<InterestOption>,
        selectedInterestIds: (results[1] as List<InterestOption>)
            .map((i) => i.id)
            .toSet(),
      );
    } catch (_) {
      // Best-effort; leave empty on failure
    }
  }

  void toggleInterest(String id, bool selected) {
    final updated = Set<String>.from(state.selectedInterestIds);
    if (selected) {
      updated.add(id);
    } else {
      updated.remove(id);
    }
    state = state.copyWith(selectedInterestIds: updated);
  }

  Future<void> pickAndUploadPhoto(
    ImagePicker picker,
    ImageSource source,
    ProfileRepository profileRepo,
  ) async {
    state = state.copyWith(isUploadingPhoto: true, errorKey: null);
    try {
      final picked = await picker.pickImage(source: source, imageQuality: 85);
      if (picked == null) return;
      final user = await profileRepo.uploadPhoto(picked.path);
      state = state.copyWith(photoUrl: user.photoUrl);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    } finally {
      state = state.copyWith(isUploadingPhoto: false);
    }
  }

  Future<void> onBundeslandChanged(
    String? code,
    GeographyRepository geographyRepo,
  ) async {
    if (code == null) {
      state = state.copyWith(
        selectedBundesland: null,
        selectedBezirk: null,
        selectedGemeinde: null,
        bezirke: [],
        gemeinden: [],
      );
      return;
    }
    state = state.copyWith(
      selectedBundesland: code,
      selectedBezirk: null,
      selectedGemeinde: null,
      bezirke: [],
      gemeinden: [],
    );
    try {
      final bezirke = await geographyRepo.getBezirke(code);
      state = state.copyWith(bezirke: bezirke);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    }
  }

  Future<void> onBezirkChanged(
    String? code,
    GeographyRepository geographyRepo,
  ) async {
    if (code == null) {
      state = state.copyWith(
        selectedBezirk: null,
        selectedGemeinde: null,
        gemeinden: [],
      );
      return;
    }
    state = state.copyWith(
      selectedBezirk: code,
      selectedGemeinde: null,
      gemeinden: [],
    );
    try {
      final gemeinden = await geographyRepo.getGemeinden(code);
      state = state.copyWith(gemeinden: gemeinden);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    }
  }

  void onGemeindeChanged(String? code, double? lat, double? lng) {
    state = state.copyWith(
      selectedGemeinde: code,
      latitude: lat,
      longitude: lng,
    );
  }

  Future<void> geocodeAddress(
    String fullAddress,
    GeographyRepository geographyRepo,
  ) async {
    state = state.copyWith(isGeocoding: true, errorKey: null);
    try {
      final result = await geographyRepo.geocodeAddress(
        fullAddress,
        persistToProfile: true,
      );
      state = state.copyWith(
        latitude: result.latitude,
        longitude: result.longitude,
      );
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    } finally {
      state = state.copyWith(isGeocoding: false);
    }
  }

  void setPhoneVerified() => state = state.copyWith(phoneVerified: true);
  void setVerifyingPhone(bool v) =>
      state = state.copyWith(isVerifyingPhone: v, errorKey: null);
  void setError(String key) => state = state.copyWith(errorKey: key);
  void clearError() => state = state.copyWith(errorKey: null);
  void setLocale(String locale) =>
      state = state.copyWith(preferredLocale: locale);
  void setSeniorMode(bool v) => state = state.copyWith(seniorModeDefault: v);

  Future<void> save(
    String displayName,
    ProfileRepository profileRepo,
    InterestsRepository interestsRepo,
  ) async {
    state = state.copyWith(isSaving: true, errorKey: null);
    try {
      await Future.wait([
        profileRepo.updateProfile(
          displayName: displayName,
          preferredLocale: state.preferredLocale,
          seniorModeDefault: state.seniorModeDefault,
        ),
        interestsRepo.updateSelected(state.selectedInterestIds.toList()),
      ]);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    } finally {
      state = state.copyWith(isSaving: false);
    }
  }
}
