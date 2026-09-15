import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../shared/riverpod/async_state.dart';
import '../../auth/data/auth_repository.dart';
import '../data/interests_repository.dart';
import '../data/profile_repository.dart';

class ProfileViewData {
  const ProfileViewData({
    required this.user,
    required this.interests,
  });

  final UserSummary user;
  final List<InterestOption> interests;

  ProfileViewData copyWith({
    UserSummary? user,
    List<InterestOption>? interests,
  }) {
    return ProfileViewData(
      user: user ?? this.user,
      interests: interests ?? this.interests,
    );
  }
}

class ProfileViewNotifier extends StateNotifier<AsyncState<ProfileViewData>> {
  ProfileViewNotifier({
    required this.profileRepository,
    required this.authRepository,
    required this.interestsRepository,
  }) : super(const AsyncState.loading()) {
    load();
  }

  final ProfileRepository profileRepository;
  final AuthRepository authRepository;
  final InterestsRepository interestsRepository;

  Future<void> load() async {
    state = const AsyncState.loading();
    try {
      final results = await Future.wait([
        profileRepository.getCurrentUser(),
        interestsRepository.getSelected(),
      ]);
      final user = results[0] as UserSummary;
      final interests = results[1] as List<InterestOption>;
      state = AsyncState.loaded(ProfileViewData(user: user, interests: interests));
    } catch (e, st) {
      state = AsyncState.error('errors.generic', e, st);
    }
  }

  Future<void> logout() async {
    await authRepository.logout();
  }
}

final profileViewProvider = StateNotifierProvider.autoDispose
    .family<ProfileViewNotifier, AsyncState<ProfileViewData>, ApiClient>(
  (ref, apiClient) {
    return ProfileViewNotifier(
      profileRepository: ProfileRepositoryImpl(apiClient),
      authRepository: AuthRepositoryImpl(apiClient),
      interestsRepository: InterestsRepositoryImpl(apiClient),
    );
  },
);
