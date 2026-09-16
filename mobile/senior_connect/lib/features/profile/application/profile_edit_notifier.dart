import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../auth/data/auth_repository.dart';
import '../data/geography_repository.dart';
import '../data/interests_repository.dart';
import '../data/profile_repository.dart';

class ProfileEditState {
  const ProfileEditState({
    this.isLoading = true,
    this.isSaving = false,
    this.isVerifyingPhone = false,
    this.isGeocoding = false,
    this.isUploadingPhoto = false,
    this.phoneVerified = false,
    this.errorKey,
    this.user,
    this.bundeslaender = const [],
    this.bezirke = const [],
    this.gemeinden = const [],
    this.interestCatalog = const [],
    this.selectedInterestIds = const {},
    this.selectedBundesland,
    this.selectedBezirk,
    this.selectedGemeinde,
    this.latitude,
    this.longitude,
    this.preferredLocale = 'de',
    this.seniorModeDefault = false,
    this.photoUrl,
  });

  final bool isLoading;
  final bool isSaving;
  final bool isVerifyingPhone;
  final bool isGeocoding;
  final bool isUploadingPhoto;
  final bool phoneVerified;
  final String? errorKey;
  final UserSummary? user;
  final List<Bundesland> bundeslaender;
  final List<Bezirk> bezirke;
  final List<Gemeinde> gemeinden;
  final List<InterestOption> interestCatalog;
  final Set<String> selectedInterestIds;
  final String? selectedBundesland;
  final String? selectedBezirk;
  final String? selectedGemeinde;
  final double? latitude;
  final double? longitude;
  final String preferredLocale;
  final bool seniorModeDefault;
  final String? photoUrl;

  ProfileEditState copyWith({
    bool? isLoading,
    bool? isSaving,
    bool? isVerifyingPhone,
    bool? isGeocoding,
    bool? isUploadingPhoto,
    bool? phoneVerified,
    Object? errorKey = _sentinel,
    UserSummary? user,
    List<Bundesland>? bundeslaender,
    List<Bezirk>? bezirke,
    List<Gemeinde>? gemeinden,
    List<InterestOption>? interestCatalog,
    Set<String>? selectedInterestIds,
    Object? selectedBundesland = _sentinel,
    Object? selectedBezirk = _sentinel,
    Object? selectedGemeinde = _sentinel,
    Object? latitude = _sentinel,
    Object? longitude = _sentinel,
    String? preferredLocale,
    bool? seniorModeDefault,
    Object? photoUrl = _sentinel,
  }) {
    return ProfileEditState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      isVerifyingPhone: isVerifyingPhone ?? this.isVerifyingPhone,
      isGeocoding: isGeocoding ?? this.isGeocoding,
      isUploadingPhoto: isUploadingPhoto ?? this.isUploadingPhoto,
      phoneVerified: phoneVerified ?? this.phoneVerified,
      errorKey: errorKey == _sentinel ? this.errorKey : errorKey as String?,
      user: user ?? this.user,
      bundeslaender: bundeslaender ?? this.bundeslaender,
      bezirke: bezirke ?? this.bezirke,
      gemeinden: gemeinden ?? this.gemeinden,
      interestCatalog: interestCatalog ?? this.interestCatalog,
      selectedInterestIds: selectedInterestIds ?? this.selectedInterestIds,
      selectedBundesland: selectedBundesland == _sentinel
          ? this.selectedBundesland
          : selectedBundesland as String?,
      selectedBezirk: selectedBezirk == _sentinel ? this.selectedBezirk : selectedBezirk as String?,
      selectedGemeinde: selectedGemeinde == _sentinel
          ? this.selectedGemeinde
          : selectedGemeinde as String?,
      latitude: latitude == _sentinel ? this.latitude : latitude as double?,
      longitude: longitude == _sentinel ? this.longitude : longitude as double?,
      preferredLocale: preferredLocale ?? this.preferredLocale,
      seniorModeDefault: seniorModeDefault ?? this.seniorModeDefault,
      photoUrl: photoUrl == _sentinel ? this.photoUrl : photoUrl as String?,
    );
  }
}

const _sentinel = Object();

class ProfileEditNotifier extends StateNotifier<ProfileEditState> {
  ProfileEditNotifier({
    required this.profileRepository,
    required this.authRepository,
    required this.geographyRepository,
    required this.interestsRepository,
  }) : super(const ProfileEditState()) {
    initialize();
  }

  final ProfileRepository profileRepository;
  final AuthRepository authRepository;
  final GeographyRepository geographyRepository;
  final InterestsRepository interestsRepository;

  Future<void> initialize() async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      final results = await Future.wait([
        profileRepository.getCurrentUser(),
        geographyRepository.getBundeslaender(),
        interestsRepository.getCatalog(),
        interestsRepository.getSelected(),
      ]);

      final user = results[0] as UserSummary;
      final bundeslaender = results[1] as List<Bundesland>;
      final catalog = results[2] as List<InterestOption>;
      final selected = results[3] as List<InterestOption>;

      state = state.copyWith(
        isLoading: false,
        user: user,
        bundeslaender: bundeslaender,
        interestCatalog: catalog,
        selectedInterestIds: selected.map((i) => i.id).toSet(),
        preferredLocale: user.preferredLocale,
        seniorModeDefault: user.seniorModeDefault,
        photoUrl: user.photoUrl,
      );
    } catch (_) {
      state = state.copyWith(isLoading: false, errorKey: 'errors.generic');
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

  void setPreferredLocale(String locale) {
    state = state.copyWith(preferredLocale: locale);
  }

  void setSeniorModeDefault(bool value) {
    state = state.copyWith(seniorModeDefault: value);
  }

  void setPhoneVerified(bool verified) {
    state = state.copyWith(phoneVerified: verified);
  }

  void setCoordinates(double? lat, double? lon) {
    state = state.copyWith(latitude: lat, longitude: lon);
  }

  void setErrorKey(String? key) {
    state = state.copyWith(errorKey: key);
  }

  Future<void> uploadPhoto(String filePath) async {
    state = state.copyWith(isUploadingPhoto: true, errorKey: null);
    try {
      final user = await profileRepository.uploadPhoto(filePath);
      state = state.copyWith(isUploadingPhoto: false, photoUrl: user.photoUrl);
    } catch (_) {
      state = state.copyWith(isUploadingPhoto: false, errorKey: 'errors.generic');
    }
  }

  Future<void> onBundeslandChanged(String? code) async {
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
      final bezirke = await geographyRepository.getBezirke(code);
      state = state.copyWith(bezirke: bezirke);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    }
  }

  Future<void> onBezirkChanged(String? code) async {
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
      final gemeinden = await geographyRepository.getGemeinden(code);
      state = state.copyWith(gemeinden: gemeinden);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    }
  }

  Gemeinde? onGemeindeChanged(String? code) {
    if (code == null) {
      state = state.copyWith(
        selectedGemeinde: null,
        latitude: null,
        longitude: null,
      );
      return null;
    }

    final matches = state.gemeinden.where((g) => g.code == code);
    if (matches.isNotEmpty) {
      final gemeinde = matches.first;
      state = state.copyWith(
        selectedGemeinde: code,
        latitude: gemeinde.latitude,
        longitude: gemeinde.longitude,
      );
      return gemeinde;
    }
    return null;
  }

  Future<GeocodeResult?> geocodeAddress(String fullAddress) async {
    state = state.copyWith(isGeocoding: true, errorKey: null);
    try {
      final result = await geographyRepository.geocodeAddress(
        fullAddress,
        persistToProfile: true,
      );
      state = state.copyWith(
        isGeocoding: false,
        latitude: result.latitude,
        longitude: result.longitude,
      );
      return result;
    } catch (_) {
      state = state.copyWith(isGeocoding: false, errorKey: 'errors.generic');
      return null;
    }
  }

  Future<bool> save({required String displayName}) async {
    state = state.copyWith(isSaving: true, errorKey: null);
    try {
      await Future.wait([
        profileRepository.updateProfile(
          displayName: displayName,
          preferredLocale: state.preferredLocale,
          seniorModeDefault: state.seniorModeDefault,
        ),
        interestsRepository.updateSelected(state.selectedInterestIds.toList()),
      ]);
      state = state.copyWith(isSaving: false);
      return true;
    } catch (_) {
      state = state.copyWith(isSaving: false, errorKey: 'errors.generic');
      return false;
    }
  }
}

final profileEditProvider = StateNotifierProvider.autoDispose
    .family<ProfileEditNotifier, ProfileEditState, ApiClient>(
  (ref, apiClient) {
    return ProfileEditNotifier(
      profileRepository: ProfileRepositoryImpl(apiClient),
      authRepository: AuthRepositoryImpl(apiClient),
      geographyRepository: GeographyRepositoryImpl(apiClient),
      interestsRepository: InterestsRepositoryImpl(apiClient),
    );
  },
);
