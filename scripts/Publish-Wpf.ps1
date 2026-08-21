param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0-windows",
    [ValidateSet("fd", "sc")]
    [string]$Mode = "fd"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$wpfProj = Join-Path $root "src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj"
$version = [regex]::Match(
    (Get-Content -LiteralPath (Join-Path $root "Directory.Build.props") -Raw),
    '<VersionPrefix>([^<]+)</VersionPrefix>'
).Groups[1].Value

$modeTag = if ($Mode -eq "sc") { "selfcontained" } else { "frameworkdependent" }
$publishDir = Join-Path $root "artifacts\publish\wpf-$modeTag"

Write-Host "═══ Publicando QubiCostElectrical.Wpf ($modeTag, $Configuration, v$version) ═══" -ForegroundColor Cyan

$publishArgs = @(
    $wpfProj,
    "--configuration", $Configuration,
    "--output", $publishDir
)

if ($Mode -eq "sc") {
    $publishArgs += "--self-contained", "true"
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:EnableCompressionInSingleFile=true"
}

if (Test-Path $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publish falló" }

# checksum
$sha256 = (Get-FileHash -LiteralPath (Get-ChildItem -LiteralPath $publishDir -Filter "*.exe" | Select-Object -First 1).FullName -Algorithm SHA256).Hash.ToLower()
Write-Host "  SHA-256: $sha256" -ForegroundColor Green
Write-Host "║ Publicado en: $publishDir" -ForegroundColor Green
