import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';

class CommunityFeedScreen extends StatelessWidget {
  const CommunityFeedScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final events = [
      {
        'title': 'Senioren-Schachtreff',
        'date': 'Dienstag, 15:00 Uhr',
        'location': 'Gemeindezentrum Mitte',
        'spots': '3 Plätze frei',
      },
      {
        'title': 'Gemeinsames Kaffeetrinken & Plaudern',
        'date': 'Donnerstag, 14:30 Uhr',
        'location': 'Café Sonnenschein',
        'spots': 'Ausgebucht (Warteliste)',
      },
      {
        'title': 'Gedächtnistraining & Rätselspaß',
        'date': 'Samstag, 10:00 Uhr',
        'location': 'Stadtbibliothek',
        'spots': '5 Plätze frei',
      },
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('Gemeinschaft & Treffen'),
      ),
      body: ListView.separated(
        padding: const EdgeInsets.all(AppSpacing.md),
        itemCount: events.length,
        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
        itemBuilder: (context, index) {
          final ev = events[index];
          return Card(
            elevation: 1,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadius.md),
            ),
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    ev['title']!,
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: theme.colorScheme.primary,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Row(
                    children: [
                      Icon(Icons.calendar_today, size: 16, color: theme.colorScheme.onSurfaceVariant),
                      const SizedBox(width: 6),
                      Text(ev['date']!, style: theme.textTheme.bodyMedium),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      Icon(Icons.location_on, size: 16, color: theme.colorScheme.onSurfaceVariant),
                      const SizedBox(width: 6),
                      Text(ev['location']!, style: theme.textTheme.bodyMedium),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: theme.colorScheme.primaryContainer,
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Text(
                      ev['spots']!,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onPrimaryContainer,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}
