// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../data/discovery_repository.dart';
part 'discovery_notifier.freezed.dart';
part 'discovery_notifier.g.dart';
enum DiscoveryTab { organizations, volunteers, requests }
@freezed
class DiscoveryState with _ {
  const factory DiscoveryState({
    @Default(true) bool isLoadingLocation,
    MyLocation? myLocation,
    @Default(DiscoveryTab.organizations) DiscoveryTab tab,
    @Default(10.0) double radiusKm,
    @Default(false) bool isLoadingResults,
    List<ProximityResult>? results,
    String? errorKey,
  }) = _DiscoveryState;
}
@riverpod
class DiscoveryNotifier extends _ {
  @override
  DiscoveryState build(DiscoveryRepository repo) {
    Future(()=> _loadLocation(repo));
    return const DiscoveryState();
  }
  Future<void> _loadLocation(DiscoveryRepository repo) async {
    state = state.copyWith(isLoadingLocation: true, errorKey: null);
    try {
      final location = await repo.getMyLocation();
      state = state.copyWith(myLocation: location, isLoadingLocation: false);
      if (location != null) await _loadResults(repo);
    } catch (_) { state = state.copyWith(isLoadingLocation: false, errorKey: 'errors.generic'); }
  }
  Future<void> _loadResults(DiscoveryRepository repo) async {
    final location = state.myLocation;
    if (location == null) return;
    state = state.copyWith(isLoadingResults: true, errorKey: null);
    try {
      final results = await repo.getProximityResults(tab: state.tab, location: location, radiusKm: state.radiusKm);
      state = state.copyWith(isLoadingResults: false, results: results);
    } catch (_) { state = state.copyWith(isLoadingResults: false, errorKey: 'errors.generic'); }
  }
  void setTab(DiscoveryTab tab, DiscoveryRepository repo) {
    state = state.copyWith(tab: tab, results: null);
    _loadResults(repo);
  }
  void setRadius(double km, DiscoveryRepository repo) {
    state = state.copyWith(radiusKm: km, results: null);
    _loadResults(repo);
  }
}
