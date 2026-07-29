#Requires -Version 5.1
<#
.SYNOPSIS
    Verifica que la identidad Qubi Cost Electrical esté congelada en rutas operativas.
#>
param(
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$failures = New-Object System.Collections.Generic.List[string]

function Fail([string]$message) {
    $script:failures.Add($message)
    Write-Host "  X $message" -ForegroundColor Red
}

function Pass([string]$message) {
    Write-Host "  OK $message" -ForegroundColor Green
}

Write-Host "Verificando identidad del producto..." -ForegroundColor Cyan

$sln = Join-Path $root "QubiCostElectrical.Wpf.sln"
if (-not (Test-Path $sln)) {
    Fail "Falta solución canónica: QubiCostElectrical.Wpf.sln"
} else {
    Pass "Solución canónica presente"
}

$legacySln = Join-Path $root "PresupuestoElectrico.sln"
if (Test-Path $legacySln) {
    Fail "PresupuestoElectrico.sln aún existe; usar solo QubiCostElectrical.Wpf.sln"
}

$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch '<Product>Qubi Cost Electrical</Product>') {
    Fail "Directory.Build.props no declara Product=Qubi Cost Electrical"
} else {
    Pass "Product en Directory.Build.props"
}

$operationalScripts = @(
    "scripts\Validate-Release.ps1",
    "scripts\Publish-Release.ps1",
    "deploy\build-release.ps1",
    ".github\workflows\build.yml"
)

foreach ($rel in $operationalScripts) {
    $path = Join-Path $root $rel
    if (-not (Test-Path $path)) { continue }
    $content = Get-Content -LiteralPath $path -Raw
    if ($content -match 'PresupuestoElectrico\.sln') {
        Fail "$rel referencia PresupuestoElectrico.sln"
    }
}

$splash = Join-Path $root "src\QubiCostElectrical.Wpf\SplashWindow.xaml"
if (Test-Path $splash) {
    $splashContent = Get-Content -LiteralPath $splash -Raw
    if ($splashContent -match 'Presupuesto Eléctrico PRO|PresupuestoElectrico') {
        Fail "SplashWindow.xaml aún muestra marca antigua"
    } else {
        Pass "Splash sin marca Presupuesto Eléctrico PRO"
    }
}

$exeProject = Join-Path $root "src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj"
if (Test-Path $exeProject) {
    $proj = Get-Content -LiteralPath $exeProject -Raw
    if ($proj -notmatch 'AssemblyName.*QubiCostElectrical|QubiCostElectrical') {
        Fail "Proyecto WPF no produce QubiCostElectrical.exe"
    } else {
        Pass "Ejecutable QubiCostElectrical"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "`nIdentidad NO congelada: $($failures.Count) problema(s)." -ForegroundColor Red
    if ($Strict) { exit 1 }
    exit 1
}

Write-Host "`nIdentidad Qubi Cost Electrical verificada." -ForegroundColor Green
exit 0
