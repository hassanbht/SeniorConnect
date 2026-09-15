import 'package:flutter_riverpod/flutter_riverpod.dart';

class CreateHelpRequestState {
  const CreateHelpRequestState({
    this.selectedCategory = 'Garten & Pflanzen',
    this.isSubmitting = false,
  });

  final String selectedCategory;
  final bool isSubmitting;

  CreateHelpRequestState copyWith({
    String? selectedCategory,
    bool? isSubmitting,
  }) {
    return CreateHelpRequestState(
      selectedCategory: selectedCategory ?? this.selectedCategory,
      isSubmitting: isSubmitting ?? this.isSubmitting,
    );
  }
}

class CreateHelpRequestNotifier
    extends StateNotifier<CreateHelpRequestState> {
  CreateHelpRequestNotifier() : super(const CreateHelpRequestState());

  void setCategory(String category) {
    state = state.copyWith(selectedCategory: category);
  }

  Future<bool> submit(String title) async {
    if (title.trim().isEmpty) return false;
    state = state.copyWith(isSubmitting: true);
    await Future.delayed(const Duration(milliseconds: 600));
    state = state.copyWith(isSubmitting: false);
    return true;
  }
}

final createHelpRequestProvider = StateNotifierProvider.autoDispose<
    CreateHelpRequestNotifier, CreateHelpRequestState>(
  (ref) => CreateHelpRequestNotifier(),
);
