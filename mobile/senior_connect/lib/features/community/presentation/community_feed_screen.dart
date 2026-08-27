// lib/features/community/presentation/community_feed_screen.dart
//
// P5-06, P5-07: Community discovery feed with category filtering and spot availability.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import 'event_detail_screen.dart';

class CommunityFeedScreen extends StatefulWidget {
  const CommunityFeedScreen({
    super.key,
    this.apiClient,
  });

  final ApiClient? apiClient;

  @override
  State<CommunityFeedScreen> createState() => _CommunityFeedScreenState();
}

class _CommunityFeedScreenState extends State<CommunityFeedScreen> {
  String? _selectedCategory;
  bool _isLoading = false;
  List<Map<String, dynamic>> _events = [];

  final _categories = [
    'all',
    'sports',
    'general',
    'culture',
    'language_practice',
    'local_orientation',
  ];

  @override
  void initState() {
    super.initState();
    _loadEvents();
  }

  Future<void> _loadEvents() async {
    setState(() => _isLoading = true);

    try {
      if (widget.apiClient != null) {
        final query = _selectedCategory != null && _selectedCategory != 'all'
            ? '?category=$_selectedCategory'
            : '';
        final response = await widget.apiClient!.get<List<dynamic>>('/api/v1/community/events$query');
        if (mounted) {
          setState(() {
            _events = response.map((e) => Map<String, dynamic>.from(e as Map)).toList();
            _isLoading = false;
          });
          return;
        }
      }
    } catch (_) {}

    // Fallback demo items
    if (mounted) {
      setState(() {
        _events = [
          {
            'id': 'ev-1',
            'title': 'Senioren-Schachtreff',
            'category': 'sports',
            'date': 'Dienstag, 15:00 Uhr',
            'location': 'Gemeindezentrum Mitte',
            'spots': '3 Plätze frei',
          },
          {
            'id': 'ev-2',
            'title': 'Gemeinsames Kaffeetrinken & Plaudern',
            'category': 'general',
            'date': 'Donnerstag, 14:30 Uhr',
            'location': 'Café Sonnenschein',
            'spots': 'Ausgebucht (Warteliste)',
          },
          {
            'id': 'ev-3',
            'title': 'Gedächtnistraining & Rätselspaß',
            'category': 'culture',
            'date': 'Samstag, 10:00 Uhr',
            'location': 'Stadtbibliothek',
            'spots': '5 Plätze frei',
          },
        ];
        if (_selectedCategory != null && _selectedCategory != 'all') {
          _events = _events.where((e) => e['category'] == _selectedCategory).toList();
        }
        _isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('community.feed_title'.tr()),
      ),
      body: SafeArea(
        child: Column(
          children: [
            // Category Filter Chips
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsetsDirectional.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.sm,
              ),
              child: Row(
                children: _categories.map((cat) {
                  final isSelected = (_selectedCategory == null && cat == 'all') ||
                      _selectedCategory == cat;
                  return Padding(
                    padding: const EdgeInsetsDirectional.only(end: AppSpacing.sm),
                    child: FilterChip(
                      selected: isSelected,
                      label: Text('community.cat_$cat'.tr()),
                      onSelected: (selected) {
                        setState(() {
                          _selectedCategory = selected ? cat : null;
                        });
                        _loadEvents();
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
            Expanded(
              child: _isLoading
                  ? AppLoading(message: 'common.loading'.tr())
                  : _events.isEmpty
                      ? AppEmptyState(
                          icon: Icons.event_busy,
                          message: 'empty.no_activities'.tr(),
                        )
                      : ListView.separated(
                          padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                          itemCount: _events.length,
                          separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.md),
                          itemBuilder: (context, index) {
                            final ev = _events[index];
                            return Card(
                              elevation: 1,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(AppRadius.md),
                              ),
                              child: InkWell(
                                borderRadius: BorderRadius.circular(AppRadius.md),
                                onTap: () {
                                  if (widget.apiClient != null) {
                                    Navigator.of(context).push(
                                      MaterialPageRoute<void>(
                                        builder: (_) => EventDetailScreen(
                                          eventId: ev['id'] as String? ?? 'ev-1',
                                          apiClient: widget.apiClient!,
                                        ),
                                      ),
                                    );
                                  }
                                },
                                child: Padding(
                                  padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        ev['title'] as String? ?? '',
                                        style: theme.textTheme.titleMedium?.copyWith(
                                          fontWeight: FontWeight.bold,
                                          color: theme.colorScheme.primary,
                                        ),
                                      ),
                                      const SizedBox(height: AppSpacing.sm),
                                      if (ev['date'] != null) ...[
                                        Row(
                                          children: [
                                            Icon(Icons.calendar_today,
                                                size: 16,
                                                color: theme.colorScheme.onSurfaceVariant),
                                            const SizedBox(width: AppSpacing.xs),
                                            Text(ev['date'] as String,
                                                style: theme.textTheme.bodyMedium),
                                          ],
                                        ),
                                        const SizedBox(height: 4),
                                      ],
                                      if (ev['location'] != null) ...[
                                        Row(
                                          children: [
                                            Icon(Icons.location_on,
                                                size: 16,
                                                color: theme.colorScheme.onSurfaceVariant),
                                            const SizedBox(width: AppSpacing.xs),
                                            Text(ev['location'] as String,
                                                style: theme.textTheme.bodyMedium),
                                          ],
                                        ),
                                        const SizedBox(height: AppSpacing.sm),
                                      ],
                                      if (ev['spots'] != null)
                                        Container(
                                          padding: const EdgeInsets.symmetric(
                                              horizontal: 8, vertical: 4),
                                          decoration: BoxDecoration(
                                            color: theme.colorScheme.primaryContainer,
                                            borderRadius:
                                                BorderRadius.circular(AppRadius.sm),
                                          ),
                                          child: Text(
                                            ev['spots'] as String,
                                            style: theme.textTheme.bodySmall?.copyWith(
                                              color: theme.colorScheme.onPrimaryContainer,
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                        ),
                                    ],
                                  ),
                                ),
                              ),
                            );
                          },
                        ),
            ),
          ],
        ),
      ),
    );
  }
}
