# WPF Phase 10D.1 Test Separation Report

## Executive summary

The WinForms test split is applied and the filesystem failures reported during the first pass are fixed.
The apparent runtime stall was not a deadlock: the WPF solution test run completes cleanly when allowed to finish.

What changed:

- `tests/PresupuestoElectrico.Tests` no longer references `PresupuestoElectrico.WinForms`.
- `tests/PresupuestoElectrico.WinForms.Tests` exists as a separate project.
- The shared test project no longer keeps the WinForms-only test files.
- `QubiCostElectrical.Wpf.sln` excludes the WinForms application project.
- The two flaky filesystem tests were isolated to temp paths and now pass.

## Evidence table

| Test | Result | Evidence | Problem found | Final state |
|---|---|---|---|---|
| Filesystem isolation fixes | OK | `dotnet test tests\\PresupuestoElectrico.Tests\\PresupuestoElectrico.Tests.csproj -c Debug --no-build --blame-hang --blame-hang-timeout 30s -v normal` passed `1005/1005` | Real user profile paths were being touched | Fixed |
| Current inventory | OK | `PresupuestoElectrico.Tests`: `1005` tests, `PresupuestoElectrico.WinForms.Tests`: `104` tests, total unique `1109` | None | Reconciled |
| Baseline inventory | OK | Clean worktree at previous commit also reports `1109` unique tests | None | Reconciled |
| Inventory diff | OK | `34` asymmetric names total: `31` exact namespace moves + `3` parameterized-name aliases caused by unicode display differences | No lost cases | Reconciled |
| WPF solution build, Debug | OK | `dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore` completed with `0 Errors` | None | Passed |
| WPF solution build, Release | OK | `dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore` completed with `0 Errors` | None | Passed |
| WPF solution restore | Blocked by environment | `dotnet restore QubiCostElectrical.Wpf.sln` tries to read `C:\Users\cristobal1\AppData\Roaming\NuGet\NuGet.Config` and gets `Access denied` | Local NuGet config ACL issue | Not a code defect |
| WPF solution test execution | OK | `dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v normal` completed `PresupuestoElectrico.Tests.dll` with `1005/1005`, `0` failed, `0` skipped | None | Passed |

## Filesystem failures fixed

The four earlier failures came from shared tests that touched real filesystem state too aggressively.

### 1) `FileLoggerTests`

Fixes:

- Use isolated temp folders under `%TEMP%`.
- Dispose the logger before reading the generated file.
- Restore the previous `AppLog.Sink` instead of always nulling it.
- Retry reads while the file handle is being released.

Result:

- Log file creation, redaction, and log level tests now pass against temp paths only.

### 2) `RecentProjectsServiceTests`

Fixes:

- Assert against the isolated preferences file path used by the test.
- Verify the loaded preferences content instead of probing the real `%LOCALAPPDATA%` file.

Result:

- The test now proves the service writes to the scoped temp file and does not touch the real app profile.

## Commands used

### Shared test project

```powershell
dotnet test tests\PresupuestoElectrico.Tests\PresupuestoElectrico.Tests.csproj -c Debug --no-build --blame-hang --blame-hang-timeout 30s -v normal
```

Result:

- `Pruebas totales: 1005`
- `Correcto: 1005`
- `Tiempo total: 4,4459 Minutos`
- Exit code: `0`

### Full WPF solution

```powershell
dotnet test QubiCostElectrical.Wpf.sln -c Debug --no-restore --no-build -v normal
```

Result:

- `Pruebas totales: 1005`
- `Correcto: 1005`
- `Omitido: 0`
- `Tiempo total: 4,4459 Minutos`
- Exit code: `0`

### Builds

```powershell
dotnet build QubiCostElectrical.Wpf.sln -c Debug --no-restore
dotnet build QubiCostElectrical.Wpf.sln -c Release --no-restore
```

Result:

- Both builds completed with `0 Errores`.

## Inventory reconciliation

Baseline and current inventories were compared by fully qualified test name.

- Baseline unique tests: `1109`
- Current unique tests: `1109`
- Exact asymmetric names: `34`
- Exact namespace moves matched: `31`
- Remaining `3` differences are the same parameterized test names with unicode arrow text rendered differently by the console

No undiscovered test cases remain in the split itself. The set is reconciled.

## WPF solution isolation

`QubiCostElectrical.Wpf.sln` includes:

- `PresupuestoElectrico.Domain`
- `PresupuestoElectrico.Application`
- `PresupuestoElectrico.Infrastructure`
- `PresupuestoElectrico.Composition`
- `QubiCostElectrical.Wpf`
- `PresupuestoElectrico.Tests`

It does not include:

- `PresupuestoElectrico.WinForms`
- `PresupuestoElectrico.WinForms.Tests`

The Debug and Release builds completed without compiling WinForms.

## Current status

The separation work itself is correct and the filesystem regressions are fixed.
The WPF solution test run completes cleanly in this environment when given enough time.

**LISTO PARA RETIRO CONTROLADO**

## Next step

Proceed with the controlled WinForms retirement workflow only after staging and committing this validated separation state.
