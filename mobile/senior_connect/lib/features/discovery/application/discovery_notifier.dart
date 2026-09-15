import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/discovery_repository.dart';

enum DiscoveryTab { organizations, volunteers, requests }

class DiscoveryState {
  const DiscoveryState({
    this.isLoadingLocation = true,
    this.myLocation,
    this.tab = DiscoveryTab.organizations,
    this.radiusKm = 10.0,
    this.isLoadingResults = false,
    this.errorKey,
    this.results,
  });

  final bool isLoadingLocation;
  final MyLocation? myLocation;
  final DiscoveryTab tab;
  final double radiusKm;
  final bool isLoadingResults;
  final String? errorKey;
  final List<ProximityResult>? results;

  DiscoveryState copyWith({
    bool? isLoadingLocation,
    MyLocation? myLocation,
    DiscoveryTab? tab,
    double? radiusKm,
    bool? isLoadingResults,
    String? errorKey,
    List<ProximityResult>? results,
  }) {
    return DiscoveryState(
      isLoadingLocation: isLoadingLocation ?? this.isLoadingLocation,
      myLocation: myLocation ?? this.myLocation,
      tab: tab ?? this.tab,
      radiusKm: radiusKm ?? this.radiusKm,
      isLoadingResults: isLoadingResults ?? this.isLoadingResults,
      errorKey: errorKey,
      results: results ?? this.results,
    );
  }
}

class DiscoveryNotifier extends StateNotifier<DiscoveryState> {
  DiscoveryNotifier({
    required this.apiClient,
    DiscoveryRepository? repository,
  })  : _repository = repository ?? DiscoveryRepositoryImpl(apiClient),
        super(const DiscoveryState()) {
    loadLocation();
  }

  final ApiClient apiClient;
  final DiscoveryRepository _repository;

  Future<void> loadLocation() async {
    state = state.copyWith(isLoadingLocation: true, errorKey: null);
    try {
      final location = await _repository.getMyLocation();
      state = state.copyWith(
        myLocation: location,
        isLoadingLocation: false,
      );
      if (location != null) {
        await loadResults();
      }
    } catch (_) {
      state = state.copyWith(
        isLoadingLocation: false,
        errorKey: 'errors.generic',
      );
    }
  }

  Future<void> loadResults() async {
    final location = state.myLocation;
    if (location == null) return;

    state = state.copyWith(isLoadingResults: true, errorKey: null);

    try {
      final results = await switch (state.tab) {
        DiscoveryTab.organizations => _repository.getNearbyOrganizations(
            location.latitude, location.longitude, state.radiusKm),
        DiscoveryTab.volunteers => _repository.getNearbyVolunteers(
            location.latitude, location.longitude, state.radiusKm),
        DiscoveryTab.requests => _repository.getNearbyRequests(
            location.latitude, location.longitude, state.radiusKm),
      };
      state = state.copyWith(results: results, isLoadingResults: false);
    } on DioException catch (e) {
      state = state.copyWith(
        isLoadingResults: false,
        errorKey: mapDioError(e).l10nKey,
      );
    } catch (_) {
      state = state.copyWith(
        isLoadingResults: false,
        errorKey: 'errors.generic',
      );
    }
  }

  void setTab(DiscoveryTab tab) {
    if (tab == state.tab) return;
    state = state.copyWith(tab: tab, results: null);
    loadResults();
  }

  void setRadius(double radiusKm) {
    state = state.copyWith(radiusKm: radiusKm);
  }
}

final discoveryProvider = StateNotifierProvider.autoDispose
    .family<DiscoveryNotifier, DiscoveryState, ApiClient>(
  (ref, apiClient) => DiscoveryNotifier(apiClient: apiClient),
);
