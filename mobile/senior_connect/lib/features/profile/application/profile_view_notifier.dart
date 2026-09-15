// lib/features/profile/application/profile_view_notifier.dart
//
// Riverpod Notifier for ProfileViewScreen.
// ⚠ DO NOT add setState to the presentation layer. See AGENTS.md §State Management.

import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../shared/riverpod/async_state.dart';
import '../data/interests_repository.dart';
import '../data/profile_repository.dart';

part 'profile_view_notifier.g.dart';

class ProfileViewData {
  const ProfileViewData({required this.user, required this.interests});
  final UserSummary user;
  final List<InterestOption> interests;
}

@riverpod
class ProfileViewNotifier extends _$ProfileViewNotifier {
  @override
  AsyncState<ProfileViewData> build(
    ProfileRepository profileRepo,
    InterestsRepository interestsRepo,
  ) {
    Future(() => load(profileRepo, interestsRepo));
    return const AsyncState.loading();
  }

  Future<void> load(
    ProfileRepository profileRepo,
    InterestsRepository interestsRepo,
  ) async {
    state = const AsyncState.loading();
    try {
      final results = await Future.wait([
        profileRepo.getCurrentUser(),
        interestsRepo.getSelected(),
      ]);
      final user = results[0] as UserSummary;
      final interests = results[1] as List<InterestOption>;
      state = AsyncState.loaded(
        ProfileViewData(user: user, interests: interests),
      );
    } catch (_) {
      state = const AsyncState.error('errors.generic');
    }
  }
}
