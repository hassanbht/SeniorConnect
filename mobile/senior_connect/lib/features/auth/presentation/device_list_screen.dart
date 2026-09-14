// lib/features/auth/presentation/device_list_screen.dart
//
// P1-10: Active device sessions with per-device revoke.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';

class DeviceListScreen extends StatefulWidget {
  const DeviceListScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<DeviceListScreen> createState() => _DeviceListScreenState();
}

class _DeviceListScreenState extends State<DeviceListScreen> {
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);

  List<DeviceSession>? _devices;
  bool _isLoading = true;
  String? _errorKey;
  String? _revokingId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });
    try {
      final devices = await _authRepository.getDevices();
      if (mounted) setState(() => _devices = devices);
    } catch (_) {
      if (mounted) setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _revoke(DeviceSession device) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('devices.revoke_confirm_title'.tr()),
        content: Text('devices.revoke_confirm_desc'.tr()),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text('common.cancel'.tr())),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: Text('devices.revoke'.tr())),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _revokingId = device.id);
    try {
      await _authRepository.revokeDevice(device.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('devices.revoked'.tr())),
        );
        await _load();
      }
    } catch (_) {
      if (mounted) setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _revokingId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: Text('devices.title'.tr())),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _errorKey != null && (_devices == null || _devices!.isEmpty)
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_errorKey!.tr(), textAlign: TextAlign.center),
                      const SizedBox(height: 16),
                      FilledButton(onPressed: _load, child: Text('common.retry'.tr())),
                    ],
                  ),
                )
              : (_devices == null || _devices!.isEmpty)
                  ? Center(child: Text('devices.empty'.tr()))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: _devices!.length,
                        separatorBuilder: (_, __) => const Divider(),
                        itemBuilder: (context, index) {
                          final device = _devices![index];
                          return ListTile(
                            leading: Icon(
                              device.isPersonalDevice ? Icons.smartphone : Icons.devices_other,
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
                                : _revokingId == device.id
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
                                          onPressed: () => _revoke(device),
                                        ),
                                      ),
                          );
                        },
                      ),
                    ),
    );
  }
}
