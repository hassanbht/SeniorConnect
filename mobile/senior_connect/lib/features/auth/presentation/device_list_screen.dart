// lib/features/auth/presentation/device_list_screen.dart
//
// P1-10: Active device sessions with per-device revoke.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../application/device_list_notifier.dart';
import '../data/auth_repository.dart';

class DeviceListScreen extends ConsumerWidget {
  const DeviceListScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  Future<void> _revoke(
    BuildContext context,
    WidgetRef ref,
    DeviceSession device,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('devices.revoke_confirm_title'.tr()),
        content: Text('devices.revoke_confirm_desc'.tr()),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text('common.cancel'.tr()),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text('devices.revoke'.tr()),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    final notifier = ref.read(deviceListProvider(apiClient).notifier);
    final success = await notifier.revokeDevice(device.id);
    if (success && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('devices.revoked'.tr())),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colorScheme = Theme.of(context).colorScheme;
    final deviceState = ref.watch(deviceListProvider(apiClient));
    final notifier = ref.read(deviceListProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(title: Text('devices.title'.tr())),
      body: deviceState.devicesState.when(
        initial: () => const Center(child: CircularProgressIndicator()),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (message, _) => Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(message.tr(), textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: notifier.load,
                child: Text('common.retry'.tr()),
              ),
            ],
          ),
        ),
        empty: (_) => Center(child: Text('devices.empty'.tr())),
        loaded: (devices) => RefreshIndicator(
          onRefresh: notifier.load,
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: devices.length,
            separatorBuilder: (_, _) => const Divider(height: 1),
            itemBuilder: (context, index) {
              final device = devices[index];
              return ListTile(
                leading: Icon(
                  device.isPersonalDevice
                      ? Icons.smartphone
                      : Icons.devices_other,
                  color: colorScheme.primary,
                ),
                title: Text(
                  device.deviceLabel?.isNotEmpty == true
                      ? device.deviceLabel!
                      : (device.isPersonalDevice
                          ? 'devices.personal_device'.tr()
                          : 'devices.other_device'.tr()),
                ),
                subtitle: Text(
                  device.isCurrent
                      ? 'devices.current'.tr()
                      : 'devices.issued_on'.tr(namedArgs: {
                          'date': DateFormat.yMMMd(context.locale.toString())
                              .format(device.issuedAtUtc.toLocal()),
                        }),
                ),
                trailing: device.isCurrent
                    ? null
                    : deviceState.revokingId == device.id
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Semantics(
                            button: true,
                            label: 'devices.revoke_semantic'.tr(),
                            child: IconButton(
                              icon: Icon(Icons.logout, color: colorScheme.error),
                              onPressed: () => _revoke(context, ref, device),
                            ),
                          ),
              );
            },
          ),
        ),
      ),
    );
  }
}
