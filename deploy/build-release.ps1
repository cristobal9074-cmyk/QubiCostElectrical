#Requires -Version 5.1
<#
.SYNOPSIS
    Construye y empaqueta Qubi Cost Electrical para distribución con Velopack.

.DESCRIPTION
    1. Publica el binario win-x64 self-contained.
    2. Empaqueta con `vpk` (Velopack CLI).
    3. (Opcional) Sube el release a GitHub con `gh`.

    Requisitos previos:
        dotnet SDK 8+
        vpk      → dotnet tool install -g vpk
        gh       → https://cli.github.com  (solo para --publish)

    Sin firma de código: SmartScreen mostrará advertencia. Para distribución comercial,
    configure CODE_SIGNING_CERT_PFX y use -Sign (ver docs/RELEASE_PROCESS.md).

.PARAMETER Sign
    Firma los artefactos Velopack con Authenticode si hay certificado configurado.

.PARAMETER Version
    Versión completa a empaquetar (ej: "1.0.0-rc.1"). Si se omite, se compone desde
    VersionPrefix + VersionSuffix en Directory.Build.props.

.PARAMETER Publish
    Si se indica, sube el release a GitHub usando `gh release create`.

.PARAMETER GitHubRepo
    Repo en formato "usuario/repo". Requerido si se usa -Publish.
    Ejemplo: "smingspa/qubi-cost-electrical"

.PARAMETER KeepReleases
    Si se indica, no borra el directorio de releases antes de empaquetar.
    Útil para acumular RC.1 y RC.2 en el mismo feed local de actualización.

.EXAMPLE
    .\deploy\build-release.ps1
    .\deploy\build-release.ps1 -Version "1.0.0-rc.1"
    .\deploy\build-release.ps1 -KeepReleases
    .\deploy\build-release.ps1 -Publish -GitHubRepo "smingspa/qubi-cost-electrical"
#>
param(
    [string]$Version      = "",
    [switch]$Publish,
    [string]$GitHubRepo   = "",
    [switch]$KeepReleases,
    [switch]$Sign
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root        = Split-Path $PSScriptRoot -Parent
$project     = Join-Path $root "src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj"
$publishDir  = Join-Path $root "deploy\publish"
$releasesDir = Join-Path $root "deploy\releases"

# ── Versión ──────────────────────────────────────────────────────────────────
if (-not $Version) {
    $props         = [xml](Get-Content (Join-Path $root "Directory.Build.props"))
    $versionPrefix = $props.Project.PropertyGroup.VersionPrefix
    $versionSuffix = $props.Project.PropertyGroup.VersionSuffix
    if (-not $versionPrefix) { throw "No se encontró VersionPrefix en Directory.Build.props" }
    $Version = if ($versionSuffix) { "$versionPrefix-$versionSuffix" } else { $versionPrefix }
}
Write-Host "==> Versión: $Version" -ForegroundColor Cyan

# ── Verificar herramientas ───────────────────────────────────────────────────
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "Instalando Velopack CLI (vpk)..." -ForegroundColor Yellow
    dotnet tool install -g vpk
    if ($LASTEXITCODE -ne 0) { throw "No se pudo instalar vpk. Instálalo manualmente: dotnet tool install -g vpk" }
}

if ($Publish -and -not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh (GitHub CLI) no está instalado. Descárgalo de https://cli.github.com"
}
if ($Publish -and -not $GitHubRepo) {
    throw "Se requiere -GitHubRepo cuando se usa -Publish. Ej: -GitHubRepo 'smingspa/presupuesto-electrico-pro'"
}

# ── Limpiar carpetas de salida ───────────────────────────────────────────────
Write-Host "==> Limpiando carpetas de salida..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (-not $KeepReleases -and (Test-Path $releasesDir)) {
    Remove-Item $releasesDir -Recurse -Force
}

# ── Publicar ─────────────────────────────────────────────────────────────────
Write-Host "==> Publicando win-x64 self-contained..." -ForegroundColor Cyan
dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    --output $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló (código $LASTEXITCODE)" }

# ── Empaquetar con Velopack ───────────────────────────────────────────────────
Write-Host "==> Empaquetando con vpk..." -ForegroundColor Cyan
vpk pack `
    --packId      "QubiCostElectrical" `
    --packVersion $Version `
    --packDir     $publishDir `
    --packTitle   "Qubi Cost Electrical" `
    --mainExe     "QubiCostElectrical.exe" `
    --outputDir   $releasesDir `
    --channel     win

if ($LASTEXITCODE -ne 0) { throw "vpk pack falló (código $LASTEXITCODE)" }

if ($Sign) {
    Write-Host "==> Firmando artefactos..." -ForegroundColor Cyan
    $signScript = Join-Path $PSScriptRoot "Sign-Authenticode.ps1"
    $artifacts = Get-ChildItem $releasesDir -File -Include *.exe,*.nupkg
    if ($artifacts.Count -gt 0) {
        & $signScript -Paths ($artifacts | ForEach-Object { $_.FullName })
        if ($LASTEXITCODE -eq 1) { throw "Firma Authenticode falló" }
        if ($LASTEXITCODE -eq 2) {
            Write-Host "Firma omitida: configure CODE_SIGNING_CERT_PFX y CODE_SIGNING_CERT_PASSWORD" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "==> Release v$Version listo en: $releasesDir" -ForegroundColor Green
Get-ChildItem $releasesDir | Select-Object Name, @{N='MB';E={[math]::Round($_.Length/1MB,1)}}

# ── Publicar en GitHub ────────────────────────────────────────────────────────
if ($Publish) {
    Write-Host ""
    Write-Host "==> Publicando en GitHub ($GitHubRepo)..." -ForegroundColor Cyan
    $tag   = "v$Version"
    $files = (Get-ChildItem $releasesDir -File).FullName

    gh release create $tag @files `
        --repo  $GitHubRepo `
        --title "Qubi Cost Electrical $tag" `
        --notes "## Cambios en $tag`n`nVer CHANGELOG para detalle."

    if ($LASTEXITCODE -ne 0) { throw "gh release create falló" }
    Write-Host "Release publicado: https://github.com/$GitHubRepo/releases/tag/$tag" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Para publicar en GitHub, ejecuta:" -ForegroundColor Yellow
    Write-Host "  .\deploy\build-release.ps1 -Publish -GitHubRepo 'usuario/repo'" -ForegroundColor Yellow
}
