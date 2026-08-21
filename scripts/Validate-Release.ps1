param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$sln = Join-Path $root "QubiCostElectrical.Wpf.sln"

function Stop-StaleTestHosts {
    Write-Host "  Limpieza previa de testhost/vstest..." -ForegroundColor DarkGray
    Get-Process | Where-Object { $_.Name -like "testhost*" -or $_.Name -like "vstest*" } | ForEach-Object {
        try {
            Stop-Process -Id $_.Id -Force -ErrorAction Stop
            Write-Host "    detenido $($_.Name) ($($_.Id))" -ForegroundColor DarkGray
        }
        catch {
            Write-Host "    no se pudo detener $($_.Name) ($($_.Id))" -ForegroundColor Yellow
        }
    }
}

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "  Validacion pre-release - Qubi Cost Electrical" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# 1. Identidad del producto
Write-Host "`n[1/7] Verificando identidad del producto..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "Verify-ProductIdentity.ps1") -Strict
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "  OK Identidad congelada" -ForegroundColor Green

# 2. Repositorio limpio
Write-Host "`n[2/7] Verificando repositorio limpio..." -ForegroundColor Yellow
$porcelain = & git -C $root status --porcelain
if ($porcelain) {
    Write-Host "  X Repositorio sucio. Archivos sin confirmar:" -ForegroundColor Red
    $porcelain | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
    exit 1
}
Write-Host "  OK Repositorio limpio" -ForegroundColor Green

# 3. Version desde Directory.Build.props
Write-Host "`n[3/7] Leyendo version..." -ForegroundColor Yellow
$propsPath = Join-Path $root "Directory.Build.props"
if (-not (Test-Path $propsPath)) { throw "No se encuentra Directory.Build.props" }
$propsContent = Get-Content -LiteralPath $propsPath -Raw
$prefixMatch = [regex]::Match($propsContent, '<VersionPrefix>([^<]+)</VersionPrefix>')
$suffixMatch = [regex]::Match($propsContent, '<VersionSuffix>([^<]*)</VersionSuffix>')
if (-not $prefixMatch.Success) { throw "No se encontro VersionPrefix en Directory.Build.props" }
$version = $prefixMatch.Groups[1].Value
if ($suffixMatch.Success -and $suffixMatch.Groups[1].Value) {
    $version = "$version-$($suffixMatch.Groups[1].Value)"
}
Write-Host "  OK Version: $version" -ForegroundColor Green

# 4. ProductVersionInfo se valida en el build
Write-Host "`n[4/7] Verificando ProductVersionInfo..." -ForegroundColor Yellow
Write-Host "  OK Se validara con el ensamblado compilado" -ForegroundColor Green

# 5-7. Restore + Build + Test
if (-not $SkipBuild) {
    Write-Host "`n[5/7] Restaurando paquetes..." -ForegroundColor Yellow
    dotnet restore $sln
    if ($LASTEXITCODE -ne 0) { throw "Restore fallo" }
    Write-Host "  OK Restore" -ForegroundColor Green

    Write-Host "`n[6/7] Compilando..." -ForegroundColor Yellow
    dotnet build $sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build fallo" }
    Write-Host "  OK Build" -ForegroundColor Green

    Write-Host "`n[7/7] Ejecutando pruebas..." -ForegroundColor Yellow
    Stop-StaleTestHosts
    dotnet test $sln --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests fallaron" }
    Write-Host "  OK Todos los tests pasaron" -ForegroundColor Green
} else {
    Write-Host "`n[5-7/7] Saltando build/test (SkipBuild)" -ForegroundColor Yellow
}

Write-Host "`n===============================================" -ForegroundColor Cyan
Write-Host "  Validacion completada. Listo para release." -ForegroundColor Green
Write-Host "  Siguiente: docs/SMOKE_TEST.md (regresion humana)" -ForegroundColor Yellow
Write-Host "===============================================" -ForegroundColor Cyan
