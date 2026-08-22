import 'package:flutter/material.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_tokens.dart';
import '../../../shared/widgets/app_button.dart';

class LogActivityScreen extends StatefulWidget {
  final VoidCallback? onLogged;

  const LogActivityScreen({super.key, this.onLogged});

  @override
  State<LogActivityScreen> createState() => _LogActivityScreenState();
}

class _LogActivityScreenState extends State<LogActivityScreen> {
  int _durationMinutes = 60;
  String _selectedCategory = 'Gartenarbeit & Pflanzen';
  String _insuranceContext = 'CoveredByOrganization';
  final _notesController = TextEditingController();
  bool _isSubmitting = false;

  final List<String> _categories = [
    'Gartenarbeit & Pflanzen',
    'Einkaufen & Besorgungen',
    'Technikhilfe & Smartphone',
    'Spaziergang & Begleitung',
    'Vorlesen & Unterhaltung',
  ];

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  void _submit() {
    setState(() => _isSubmitting = true);
    Future.delayed(const Duration(milliseconds: 600), () {
      if (mounted) {
        setState(() => _isSubmitting = false);
        widget.onLogged?.call();
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Einsatz erfolgreich eingetragen!'),
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
        title: const Text('Einsatz erfassen'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppTokens.paddingLg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Kategorie auswählen',
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
              'Dauer (Minuten)',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppTokens.paddingSm),
            Row(
              children: [30, 60, 90, 120].map((mins) {
                final isSelected = _durationMinutes == mins;
                return Padding(
                  padding: const EdgeInsets.only(right: AppTokens.paddingSm),
                  child: FilterChip(
                    label: Text('$mins Min'),
                    selected: isSelected,
                    onSelected: (selected) {
                      if (selected) setState(() => _durationMinutes = mins);
                    },
                  ),
                );
              }).toList(),
            ),
            const SizedBox(height: AppTokens.paddingLg),
            Text(
              'Notizen (optional)',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppTokens.paddingSm),
            TextField(
              controller: _notesController,
              decoration: const InputDecoration(
                hintText: 'Kurze Beschreibung der Unterstützung...',
                border: OutlineInputBorder(),
              ),
              maxLines: 3,
            ),
            const SizedBox(height: AppTokens.paddingXl),
            AppButton(
              label: 'Stunden jetzt speichern',
              icon: Icons.check,
              isLoading: _isSubmitting,
              onPressed: _submit,
            ),
          ],
        ),
      ),
    );
  }
}
