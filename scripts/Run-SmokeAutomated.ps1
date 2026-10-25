#Requires -Version 5.1
<#
.SYNOPSIS
    Ejecuta la parte automatizable del smoke test (build, tests, identidad, gates).
#>
param(
    [string]$Configuration = "Release",
    [switch]$CI
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$sln = Join-Path $root "QubiCostElectrical.Wpf.sln"
$startedAt = Get-Date
$results = @()

function Stop-StaleTestHosts {
    Write-Host "Limpieza previa de testhost/vstest..." -ForegroundColor DarkGray
    Get-Process | Where-Object { $_.Name -like "testhost*" -or $_.Name -like "vstest*" } | ForEach-Object {
        try {
            Stop-Process -Id $_.Id -Force -ErrorAction Stop
            Write-Host "  detenido $($_.Name) ($($_.Id))" -ForegroundColor DarkGray
        }
        catch {
            Write-Host "  no se pudo detener $($_.Name) ($($_.Id))" -ForegroundColor Yellow
        }
    }
}

function Invoke-SmokeStep {
    param(
        [string]$Name,
        [scriptblock]$Action,
        [switch]$Optional
    )
    Write-Host "`n== $Name ==" -ForegroundColor Cyan
    & $Action
    $exit = $LASTEXITCODE
    if ($exit -ne 0 -and -not $Optional) {
        throw "$Name falló (exit $exit)"
    }
    if ($exit -ne 0) {
        $script:results += "WARN  $Name (exit $exit)"
        Write-Host "  ADVERTENCIA (no bloqueante)" -ForegroundColor Yellow
    }
    else {
        $script:results += "OK  $Name"
        Write-Host "  OK" -ForegroundColor Green
    }
}

Write-Host "Smoke automatizado - Qubi Cost Electrical" -ForegroundColor Cyan
Stop-StaleTestHosts

Invoke-SmokeStep -Name "Identidad del producto" -Action {
    & (Join-Path $root "scripts\Verify-ProductIdentity.ps1") -Strict
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Invoke-SmokeStep -Name "Restore" -Action { dotnet restore $sln | Out-Host }

Invoke-SmokeStep -Name "Build $Configuration" -Action {
    dotnet build $sln -c $Configuration --no-restore | Out-Host
}

Invoke-SmokeStep -Name "Tests funcionales" -Optional:$CI -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName!~PerformanceTests" | Out-Host
}

Invoke-SmokeStep -Name "Tests de rendimiento" -Optional -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName~PerformanceTests" | Out-Host
}

Invoke-SmokeStep -Name "Gate Fase 1" -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName~WpfPhase1GateTests" | Out-Host
}

Invoke-SmokeStep -Name "Gate Fase 2" -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName~WpfPhase2GateTests" | Out-Host
}

Invoke-SmokeStep -Name "Gate Fase 3" -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName~WpfPhase3GateTests" | Out-Host
}

Invoke-SmokeStep -Name "Navegacion unificada" -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName~ShellRouteNavigationTests" | Out-Host
}

Invoke-SmokeStep -Name "Persistencia qce" -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName~SQLiteProjectRoundTripTests" | Out-Host
}

Invoke-SmokeStep -Name "Export golden Excel" -Action {
    dotnet test $sln -c $Configuration --no-build --filter "FullyQualifiedName~ExcelExportGoldenFileTests" | Out-Host
}

$seconds = [int]((Get-Date) - $startedAt).TotalSeconds
$stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

Write-Host "`n=============================================" -ForegroundColor Green
Write-Host "Smoke automatizado completado en ${seconds}s" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
$results | ForEach-Object { Write-Host "  $_" }

$reportPath = Join-Path $root "docs\SMOKE_TEST_AUTOMATED_LAST_RUN.txt"
$reportLines = @(
    "Smoke automatizado - $stamp"
    "Duracion: ${seconds}s"
    "Configuración: $Configuration"
    ""
)
foreach ($line in $results) { $reportLines += "- $line" }
$reportLines += ""
$reportLines += "Pendiente validacion humana: ver docs/SMOKE_TEST.md secciones Instalacion, Funcionalidad GUI y Aprobacion."
$reportLines | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host "`nReporte: docs/SMOKE_TEST_AUTOMATED_LAST_RUN.txt" -ForegroundColor Yellow
