// lib/features/profile/presentation/help_faq_screen.dart
//
// P7-16: In-app Help & FAQ in simple, plain German with Senior Mode compatibility.
// Provides accessible guidance on safety, requesting help, notifications, and privacy.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../shared/widgets/app_button.dart';
import '../application/help_faq_notifier.dart';

class HelpFaqScreen extends ConsumerWidget {
  const HelpFaqScreen({super.key});

  static const List<Map<String, String>> _faqItems = [
    {
      'questionKey': 'faq.q_how_to_request',
      'answerKey': 'faq.a_how_to_request',
    },
    {
      'questionKey': 'faq.q_safety_trust',
      'answerKey': 'faq.a_safety_trust',
    },
    {
      'questionKey': 'faq.q_family_access',
      'answerKey': 'faq.a_family_access',
    },
    {
      'questionKey': 'faq.q_notifications',
      'answerKey': 'faq.a_notifications',
    },
    {
      'questionKey': 'faq.q_privacy_gdpr',
      'answerKey': 'faq.a_privacy_gdpr',
    },
    {
      'questionKey': 'faq.q_emergency_144',
      'answerKey': 'faq.a_emergency_144',
    },
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final searchQuery = ref.watch(helpFaqSearchProvider);

    final filtered = _faqItems.where((item) {
      if (searchQuery.isEmpty) return true;
      final q = item['questionKey']!.tr().toLowerCase();
      final a = item['answerKey']!.tr().toLowerCase();
      final term = searchQuery.toLowerCase();
      return q.contains(term) || a.contains(term);
    }).toList();

    return Scaffold(
      appBar: AppBar(
        title: Text('faq.title'.tr()),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsetsDirectional.all(AppSpacing.md),
          children: [
            TextField(
              decoration: InputDecoration(
                hintText: 'faq.search_hint'.tr(),
                prefixIcon: const Icon(Icons.search),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
              ),
              onChanged: (val) {
                ref.read(helpFaqSearchProvider.notifier).state = val.trim();
              },
            ),
            const SizedBox(height: AppSpacing.md),
            Card(
              color: theme.colorScheme.primaryContainer,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              child: Padding(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                child: Row(
                  children: [
                    Icon(
                      Icons.help_outline,
                      color: theme.colorScheme.onPrimaryContainer,
                      size: 28,
                    ),
                    const SizedBox(width: AppSpacing.md),
                    Expanded(
                      child: Text(
                        'faq.intro_banner'.tr(),
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: theme.colorScheme.onPrimaryContainer,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            ...filtered.map((item) {
              return Card(
                margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppRadius.md),
                  side: BorderSide(color: theme.colorScheme.outlineVariant),
                ),
                child: ExpansionTile(
                  leading: Icon(
                    Icons.question_answer_outlined,
                    color: theme.colorScheme.primary,
                  ),
                  title: Text(
                    item['questionKey']!.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  childrenPadding: const EdgeInsetsDirectional.fromSTEB(
                    AppSpacing.md,
                    0,
                    AppSpacing.md,
                    AppSpacing.md,
                  ),
                  children: [
                    Align(
                      alignment: AlignmentDirectional.centerStart,
                      child: Text(
                        item['answerKey']!.tr(),
                        style: theme.textTheme.bodyLarge,
                      ),
                    ),
                  ],
                ),
              );
            }),
            const SizedBox(height: AppSpacing.lg),
            Card(
              color: theme.colorScheme.surfaceVariant,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              child: Padding(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                child: Column(
                  children: [
                    Text(
                      'faq.need_personal_help'.tr(),
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Text(
                      'faq.personal_help_desc'.tr(),
                      textAlign: TextAlign.center,
                      style: theme.textTheme.bodyMedium,
                    ),
                    const SizedBox(height: AppSpacing.md),
                    AppButton(
                      label: 'faq.contact_support'.tr(),
                      variant: AppButtonVariant.primary,
                      icon: Icons.support_agent,
                      onPressed: () {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(content: Text('faq.support_contacted'.tr())),
                        );
                      },
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
