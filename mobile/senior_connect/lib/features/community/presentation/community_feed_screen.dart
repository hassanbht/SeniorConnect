import 'package:flutter/material.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_tokens.dart';

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
        padding: const EdgeInsets.all(AppTokens.paddingMd),
        itemCount: events.length,
        separatorBuilder: (_, __) => const SizedBox(height: AppTokens.paddingMd),
        itemBuilder: (context, index) {
          final ev = events[index];
          return Card(
            elevation: 1,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppTokens.radiusMd),
            ),
            child: Padding(
              padding: const EdgeInsets.all(AppTokens.paddingMd),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    ev['title']!,
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: AppColors.primary,
                    ),
                  ),
                  const SizedBox(height: AppTokens.paddingSm),
                  Row(
                    children: [
                      const Icon(Icons.calendar_today, size: 16, color: Colors.grey),
                      const SizedBox(width: 6),
                      Text(ev['date']!, style: theme.textTheme.bodyMedium),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      const Icon(Icons.location_on, size: 16, color: Colors.grey),
                      const SizedBox(width: 6),
                      Text(ev['location']!, style: theme.textTheme.bodyMedium),
                    ],
                  ),
                  const SizedBox(height: AppTokens.paddingSm),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Text(
                      ev['spots']!,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: AppColors.primary,
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
