import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../shared/widgets/app_button.dart';
import '../application/create_help_request_notifier.dart';

class CreateHelpRequestScreen extends ConsumerStatefulWidget {
  final VoidCallback? onCreated;

  const CreateHelpRequestScreen({super.key, this.onCreated});

  @override
  ConsumerState<CreateHelpRequestScreen> createState() =>
      _CreateHelpRequestScreenState();
}

class _CreateHelpRequestScreenState
    extends ConsumerState<CreateHelpRequestScreen> {
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();

  final List<String> _categories = [
    'Garten & Pflanzen',
    'Einkaufen & Botengänge',
    'Smartphone & Computer',
    'Begleitung & Spaziergang',
    'Haushaltshilfe',
  ];

  @override
  void dispose() {
    _titleController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final title = _titleController.text;
    final success =
        await ref.read(createHelpRequestProvider.notifier).submit(title);
    if (success && mounted) {
      widget.onCreated?.call();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Hilfeanfrage erfolgreich veröffentlicht!'),
          backgroundColor: Theme.of(context).colorScheme.primary,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(createHelpRequestProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Hilfe anfragen'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Wobei benötigen Sie Unterstützung?',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppSpacing.sm),
            TextField(
              controller: _titleController,
              decoration: const InputDecoration(
                hintText: 'z.B. Glühbirne wechseln oder Rasen mähen',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'Kategorie',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.sm,
              children: _categories.map((cat) {
                final isSelected = state.selectedCategory == cat;
                return ChoiceChip(
                  label: Text(cat),
                  selected: isSelected,
                  onSelected: (selected) {
                    if (selected) {
                      ref
                          .read(createHelpRequestProvider.notifier)
                          .setCategory(cat);
                    }
                  },
                );
              }).toList(),
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'Details zur Anfrage',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppSpacing.sm),
            TextField(
              controller: _descriptionController,
              decoration: const InputDecoration(
                hintText: 'Wann passt es Ihnen am besten? Gibt es Besonderheiten?',
                border: OutlineInputBorder(),
              ),
              maxLines: 4,
            ),
            const SizedBox(height: AppSpacing.xl),
            AppButton(
              label: 'Anfrage veröffentlichen',
              icon: Icons.send,
              isLoading: state.isSubmitting,
              onPressed: _submit,
            ),
          ],
        ),
      ),
    );
  }
}
