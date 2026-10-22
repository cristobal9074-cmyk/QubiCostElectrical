param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Sign
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path (Join-Path $root "publish") $Runtime
$artifactsDir = Join-Path $root "artifacts"

Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Publicando Qubi Cost Electrical ($Runtime, $Configuration)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan

# ── 1. Leer versión ──────────────────────────────────────────────────────────
$propsPath = Join-Path $root "Directory.Build.props"
$propsContent = Get-Content -LiteralPath $propsPath -Raw
$versionMatch = [regex]::Match($propsContent, '<VersionPrefix>([^<]+)</VersionPrefix>')
$suffixMatch = [regex]::Match($propsContent, '<VersionSuffix>([^<]*)</VersionSuffix>')
$version = if ($versionMatch.Success) { $versionMatch.Groups[1].Value } else { throw "VersionPrefix no encontrado" }
if ($suffixMatch.Success -and $suffixMatch.Groups[1].Value) {
    $version = "$version-$($suffixMatch.Groups[1].Value)"
}
Write-Host "Versión: $version" -ForegroundColor Yellow

# ── 2. Limpiar publicación anterior ──────────────────────────────────────────
if (Test-Path $publishDir) {
    Write-Host "`nLimpiando publicación anterior..." -ForegroundColor Yellow
    Get-ChildItem -LiteralPath $publishDir -Force | Remove-Item -Recurse -Force
}

# ── 3. Publish self-contained ────────────────────────────────────────────────
Get-Process VBCSCompiler, dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "`nPublicando aplicación..." -ForegroundColor Yellow
dotnet publish "$root\src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj" `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:UseSharedCompilation=false `
    -nr:false `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    --output "$publishDir"
if ($LASTEXITCODE -ne 0) { throw "Publish falló" }
Write-Host "  ✓ Publicado en $publishDir" -ForegroundColor Green
# Copiar Assets (iconos y logos) a la carpeta de publicación para Windows Shell y accesos directos
$pubAssets = Join-Path $publishDir "Assets"
if (!(Test-Path $pubAssets)) { New-Item -ItemType Directory -Path $pubAssets -Force | Out-Null }
Copy-Item "$root\src\QubiCostElectrical.Wpf\Assets\*" $pubAssets -Force -Recurse
Write-Host "  ✓ Assets copiados en $pubAssets" -ForegroundColor Green

if ($Sign) {
    $exe = Join-Path $publishDir "QubiCostElectrical.exe"
    if (Test-Path $exe) {
        & (Join-Path $root "deploy\Sign-Authenticode.ps1") -Paths $exe
        if ($LASTEXITCODE -eq 1) { throw "Firma Authenticode falló" }
    }
}

# ── 4. Generar ZIP ──────────────────────────────────────────────────────────
Write-Host "`nGenerando ZIP ejecutable..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
$zipPath = Join-Path $artifactsDir "QubiCostElectrical-$version-$Runtime.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force
Write-Host "  ✓ ZIP: $zipPath" -ForegroundColor Green

# ── 5. Checksum ──────────────────────────────────────────────────────────────
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLower()
$hashPath = "$zipPath.sha256"
Set-Content -LiteralPath $hashPath -Value "$hash  $([System.IO.Path]::GetFileName($zipPath))"
Write-Host "  ✓ SHA-256: $hash" -ForegroundColor Green

# ── 6. Generar build-manifest.json ──────────────────────────────────────────
$manifest = @{
    version    = $version
    date       = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssK")
    runtime    = $Runtime
    configuration = $Configuration
    commit     = (git -C $root log --oneline -1).Split(' ')[0]
    artifacts  = @(
        @{ name = "Executable ZIP"; path = $zipPath; sha256 = $hash }
    )
} | ConvertTo-Json -Depth 3
$manifestPath = Join-Path $artifactsDir "build-manifest.json"
Set-Content -LiteralPath $manifestPath -Value $manifest
Write-Host "  ✓ Manifiesto: $manifestPath" -ForegroundColor Green

Write-Host "`n═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Publicación completada." -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
