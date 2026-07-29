<#
.SYNOPSIS
    Mide el número de líneas de todos los archivos .cs del proyecto.
    Reporta los que exceden un umbral configurable (default 300).

.DESCRIPTION
    Fase 0 — Inventario de líneas por archivo. Útil para identificar god objects
    antes y después de las fases de refactor.

.PARAMETER Threshold
    Número mínimo de líneas para incluir en el reporte (default: 300).

.PARAMETER IncludeAll
    Si se especifica, muestra todos los archivos, no solo los que exceden el umbral.

.PARAMETER Root
    Ruta raíz del proyecto. Default: raíz del repositorio (donde está este script).

.EXAMPLE
    .\tools\Measure-FileSizes.ps1

.EXAMPLE
    .\tools\Measure-FileSizes.ps1 -Threshold 500 -IncludeAll

.EXAMPLE
    .\tools\Measure-FileSizes.ps1 -Threshold 500 | Export-Csv -NoTypeInformation file_sizes.csv
#>

param(
    [int]$Threshold = 300,
    [switch]$IncludeAll,
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$repoRoot = Resolve-Path $Root
$srcDir = Join-Path $repoRoot "src"
$testDir = Join-Path $repoRoot "tests"

$results = @()

Get-ChildItem -Path $srcDir, $testDir -Recurse -Filter "*.cs" -File |
    Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' } |
    ForEach-Object {
        $lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
        $relPath = $_.FullName.Substring($repoRoot.Length + 1).TrimStart('\')
        if ($IncludeAll -or $lines -ge $Threshold) {
            $results += [PSCustomObject]@{
                File     = $relPath
                Lines    = $lines
                Over     = if ($lines -gt 500) { "GOD OBJECT" } elseif ($lines -ge $Threshold) { "WATCH" } else { "OK" }
            }
        }
    }

$results = $results | Sort-Object Lines -Descending

$godObjects = $results | Where-Object { $_.Over -eq "GOD OBJECT" }
$watchers   = $results | Where-Object { $_.Over -eq "WATCH" }

Write-Host "=== INVENTARIO DE LINEAS ===" -ForegroundColor Cyan
Write-Host "Raiz: $repoRoot" -ForegroundColor Gray
Write-Host "Umbral: $Threshold lineas`n" -ForegroundColor Gray

if ($godObjects.Count -gt 0) {
    Write-Host ">>> GOD OBJECTS (>500 lineas): $($godObjects.Count) archivos" -ForegroundColor Red
    $godObjects | Format-Table File, Lines -AutoSize | Out-String | Write-Host
}

if ($watchers.Count -gt 0) {
    Write-Host ">>> EN OBSERVACION ($Threshold-500 lineas): $($watchers.Count) archivos" -ForegroundColor Yellow
    $watchers | Format-Table File, Lines -AutoSize | Out-String | Write-Host
}

if ($IncludeAll) {
    $ok = $results | Where-Object { $_.Over -eq "OK" }
    Write-Host ">>> OK (< $Threshold lineas): $($ok.Count) archivos" -ForegroundColor Green
}

Write-Host "TOTAL: $($results.Count) archivos .cs" -ForegroundColor Cyan
Write-Host "God objects: $($godObjects.Count)" -ForegroundColor Red
Write-Host "En observacion: $($watchers.Count)" -ForegroundColor Yellow
Write-Host "OK: $(($results | Where-Object { $_.Over -eq "OK" }).Count)" -ForegroundColor Green

$results
