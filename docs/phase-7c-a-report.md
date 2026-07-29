# Phase 7C-A Report — Professional Schedule Baselines

**Date:** 2026-07-25  
**Status:** ✅ COMPLETADA

---

## Summary

Phase 7C-A implements a complete, professional-grade **Schedule Baseline Management** system for Qubi Cost Electrical, equivalent in scope and behavior to the baseline modules found in Microsoft Project and Oracle Primavera P6.

---

## Deliverables

### Domain Layer

| File | Description |
|------|-------------|
| [ScheduleBaseline.cs](../src/QubiCostElectrical.Domain/Planning/ScheduleBaseline.cs) | Baseline metadata entity (init-only, immutable after creation) |
| [ScheduleBaselineSnapshot.cs](../src/QubiCostElectrical.Domain/Planning/ScheduleBaselineSnapshot.cs) | Full schedule state snapshot |
| [ScheduleComparisonResult.cs](../src/QubiCostElectrical.Domain/Planning/ScheduleComparisonResult.cs) | Structured comparison output with ActivityDiff and DependencyDiff |
| [BaselineHasher.cs](../src/QubiCostElectrical.Domain/Planning/BaselineHasher.cs) | Deterministic SHA-256 hasher (sorted, volatile-field-excluded) |

### Application Layer

| File | Description |
|------|-------------|
| [IScheduleBaselineRepository.cs](../src/QubiCostElectrical.Application/Planning/IScheduleBaselineRepository.cs) | Repository interface for baseline metadata |
| [IScheduleBaselineSnapshotRepository.cs](../src/QubiCostElectrical.Application/Planning/IScheduleBaselineSnapshotRepository.cs) | Repository interface for snapshot data |
| [IScheduleBaselineService.cs](../src/QubiCostElectrical.Application/Planning/IScheduleBaselineService.cs) | Service interface (create, restore, compare, verify, delete, mark official) |
| [ScheduleBaselineService.cs](../src/QubiCostElectrical.Application/Planning/ScheduleBaselineService.cs) | Full service implementation (362 lines) |

### Infrastructure Layer

| File | Description |
|------|-------------|
| [PlanningStores.BaselineRepository.cs](../src/QubiCostElectrical.Infrastructure/SQLite/PlanningStores.BaselineRepository.cs) | SQLite repository implementations |
| [SQLiteSchemaMigrator.cs](../src/QubiCostElectrical.Infrastructure/SQLite/SQLiteSchemaMigrator.cs) | Migration 35 registration |
| [SqliteMigrationSteps.Methods.cs](../src/QubiCostElectrical.Infrastructure/SQLite/SqliteMigrationSteps.Methods.cs) | Migration 35 DDL: `planning_baselines` + `planning_baseline_snapshots` |

### Composition Layer

| File | Description |
|------|-------------|
| [AppServices.cs](../src/QubiCostElectrical.Composition/AppServices.cs) | `ScheduleBaseline` service property |
| [AppServicesBuilder.Methods.cs](../src/QubiCostElectrical.Composition/AppServicesBuilder.Methods.cs) | DI wiring |
| [WpfAppHost.cs](../src/QubiCostElectrical.Wpf/Infrastructure/WpfAppHost.cs) | Host initialization |

### WPF UI

| File | Description |
|------|-------------|
| [BaselineManagerView.xaml](../src/QubiCostElectrical.Wpf/Features/BaselineManager/BaselineManagerView.xaml) | Full baseline management UI (list, create, compare, restore, verify, delete, mark official) |
| [BaselineManagerViewModel.cs](../src/QubiCostElectrical.Wpf/Features/BaselineManager/BaselineManagerViewModel.cs) | ViewModel with all commands |

### Navigation Integration

| File | Change |
|------|--------|
| [ShellRoute.cs](../src/QubiCostElectrical.Wpf/Navigation/ShellRoute.cs) | Added `PlanningBaselines` route + catalog entry under "Planificación" group |
| [ShellRouteNavigation.cs](../src/QubiCostElectrical.Wpf/Navigation/ShellRouteNavigation.cs) | Added `IsPlanningRoute()` |
| [ShellViewModel.Navigation.cs](../src/QubiCostElectrical.Wpf/Shell/ShellViewModel.Navigation.cs) | Added `PlanningBaselines` case in `CreateView()` switch |
| [ShellWindow.xaml](../src/QubiCostElectrical.Wpf/Shell/ShellWindow.xaml) | Added `NavPlanificacionGroup` dropdown to navigation bar |
| [ShellWindow.xaml.Methods.cs](../src/QubiCostElectrical.Wpf/Shell/ShellWindow.xaml.Methods.cs) | Populated `NavPlanificacionGroup`, `UpdateNavVisuals`, `CloseAllDropdowns` |

### Tests

| File | Count | Description |
|------|-------|-------------|
| [BaselineTests.cs](../tests/QubiCostElectrical.Tests/Domain/Planning/BaselineTests.cs) | 4 tests | Core service behaviors (existing) |
| [BaselineCoverageTests.cs](../tests/QubiCostElectrical.Tests/Domain/Planning/BaselineCoverageTests.cs) | 23 tests | Full coverage suite (new) |
| [BaselinePersistenceTests.cs](../tests/QubiCostElectrical.Tests/Infrastructure/Planning/BaselinePersistenceTests.cs) | 3 tests | SQLite persistence (existing) |

---

## Validation Results

### Build

| Configuration | Errors | Warnings |
|---------------|--------|----------|
| Debug | 0 | 0 |
| Release | 0 | 0 |

### Tests

| Suite | Passed | Failed | Skipped | Total |
|-------|--------|--------|---------|-------|
| QubiCostElectrical.Tests (Release) | **2224** | 0 | 0 | 2224 |
| ArchitectureTests (Release) | **5** | 0 | 0 | 5 |

### Schema Version

- `DatabaseSchemaVersion` = **35**
- Migration 35: `planning_baselines` + `planning_baseline_snapshots`

---

## Test Coverage Matrix

| Requirement | Covered By |
|-------------|-----------|
| Same input → same hash | `Hash_same_input_produces_same_result` |
| Reversed order → same hash | `Hash_different_order_same_semantic_produces_same_hash` |
| Activity change → different hash | `Hash_activity_change_produces_different_hash` |
| Dependency change → different hash | `Hash_dependency_addition_produces_different_hash` |
| Alteration detected | `VerifyIntegrity_fails_and_audits_when_snapshot_tampered` |
| Sequential versions | `Version_numbers_are_sequential_starting_from_zero` |
| Official cannot be deleted | `Official_baseline_cannot_be_deleted` |
| Draft can be deleted | `Draft_baseline_can_be_deleted` |
| Restore correct | `Restore_replaces_activities_and_creates_audit_entry` |
| Restore blocked by invalid hash | `Restore_blocked_when_hash_is_invalid` |
| Restore doesn't modify baseline | `Restore_does_not_modify_baseline_metadata` |
| Integrity verify pass | `VerifyIntegrity_passes_when_snapshot_unmodified` |
| Integrity verify fail + audit | `VerifyIntegrity_fails_and_audits_when_snapshot_tampered` |
| Comparison: modified activities | `Comparison_detects_dependency_additions` |
| Comparison: critical path change | `Comparison_detects_critical_path_change` |
| Concurrency: unique IDs | `Concurrent_baseline_creation_maintains_unique_ids` |
| Cancellation token accepted | `CreateBaseline_respects_cancellation_token` |
| SHA256 stored correctly | `Baseline_sha256_is_stored_and_matches_computed_hash` |
| Permissions constants | `PlanningPermissions_has_all_required_constants` |
| Audit entries | `All_operations_create_audit_entries` |
| ViewModel model state | `ScheduleBaseline_default_status_is_draft_and_not_official` |
| Persistence: save/retrieve | `BaselineRepository_saves_and_retrieves_correctly` |
| Persistence: snapshot JSON | `BaselineSnapshotRepository_saves_and_retrieves_correctly` |
| Persistence: next version | `GetNextVersionAsync_calculates_correct_version` |

---

## WPF UI Features

- 📋 List of all baselines for the active schedule (version, name, status, created by, date, hash)
- ➕ Create new baseline (name + description)
- ⭐ Mark a draft baseline as official
- 🔍 Compare two baselines or baseline vs. current schedule
- ↩️ Restore a baseline (with integrity pre-check)
- ✅ Verify integrity on demand
- 🗑️ Delete drafts only (official baselines cannot be deleted)
- 🎨 Color-coded comparison results (green=added, red=deleted, amber=modified)

---

## Navigation

**Menu:** Planificación → Líneas base  
**Route:** `ShellRoute.PlanningBaselines`  
**Group:** `"Planificación"` (new menu group in navigation bar)
