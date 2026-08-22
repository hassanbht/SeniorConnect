import 'package:flutter/material.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../shared/widgets/app_button.dart';

class CreateHelpRequestScreen extends StatefulWidget {
  final VoidCallback? onCreated;

  const CreateHelpRequestScreen({super.key, this.onCreated});

  @override
  State<CreateHelpRequestScreen> createState() => _CreateHelpRequestScreenState();
}

class _CreateHelpRequestScreenState extends State<CreateHelpRequestScreen> {
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  String _selectedCategory = 'Garten & Pflanzen';
  bool _isSubmitting = false;

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

  void _submit() {
    if (_titleController.text.trim().isEmpty) return;

    setState(() => _isSubmitting = true);
    Future.delayed(const Duration(milliseconds: 600), () {
      if (mounted) {
        setState(() => _isSubmitting = false);
        widget.onCreated?.call();
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Hilfeanfrage erfolgreich veröffentlicht!'),
            backgroundColor: AppColors.primary,
          ),
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Hilfe anfragen'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppTokens.paddingLg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Wobei benötigen Sie Unterstützung?',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppTokens.paddingSm),
            TextField(
              controller: _titleController,
              decoration: const InputDecoration(
                hintText: 'z.B. Glühbirne wechseln oder Rasen mähen',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: AppTokens.paddingLg),
            Text(
              'Kategorie',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppTokens.paddingSm),
            Wrap(
              spacing: AppTokens.paddingSm,
              runSpacing: AppTokens.paddingSm,
              children: _categories.map((cat) {
                final isSelected = _selectedCategory == cat;
                return ChoiceChip(
                  label: Text(cat),
                  selected: isSelected,
                  onSelected: (selected) {
                    if (selected) setState(() => _selectedCategory = cat);
                  },
                );
              }).toList(),
            ),
            const SizedBox(height: AppTokens.paddingLg),
            Text(
              'Details zur Anfrage',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppTokens.paddingSm),
            TextField(
              controller: _descriptionController,
              decoration: const InputDecoration(
                hintText: 'Wann passt es Ihnen am besten? Gibt es Besonderheiten?',
                border: OutlineInputBorder(),
              ),
              maxLines: 4,
            ),
            const SizedBox(height: AppTokens.paddingXl),
            AppButton(
              label: 'Anfrage veröffentlichen',
              icon: Icons.send,
              isLoading: _isSubmitting,
              onPressed: _submit,
            ),
          ],
        ),
      ),
    );
  }
}
