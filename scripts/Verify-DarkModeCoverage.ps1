param([switch]$ReportOnly)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$designSystem = Join-Path $root "src\QubiCostElectrical.Wpf\DesignSystem"
$features = Join-Path $root "src\QubiCostElectrical.Wpf\Features"
$shell = Join-Path $root "src\QubiCostElectrical.Wpf\Shell"
$results = @()
$excludePatterns = @('\\obj\\','\\bin\\','\\DesignSystem\\')
function Should-Exclude($path) { foreach ($p in $excludePatterns) { if ($path -match $p) { return $true } }; return $false }
function Get-HexColorRefs($content) {
    $hexColors = [regex]::Matches($content, '#[0-9A-Fa-f]{6,8}')
    $namedColors = [regex]::Matches($content, '(?:Background|Foreground|BorderBrush|Fill|Stroke)="(?:White|Black|Gray|Red|Green|Blue|Yellow|Orange|Purple|Cyan|Magenta|Navy|Silver|Teal|Brown|Pink|Lime|Gold|Coral|Crimson|Indigo|Violet|Tan|Plum|Salmon|Khaki|Olive|Lavender|Azure|Ivory|Beige|Moccasin|Wheat|Peru|Sienna|Chocolate|Maroon|Tomato|Orchid|Thistle|Honeydew|Snow|MintCream|Linen|LavenderBlush|MistyRose|OldLace|PapayaWhip|SeaShell|AliceBlue|FloralWhite|GhostWhite|WhiteSmoke|LightGray|DarkGray|DimGray|SlateGray|LightSlateGray|DarkSlateGray)')
    return @($hexColors) + @($namedColors)
}
Write-Host "Verificando cobertura de modo oscuro..." -ForegroundColor Cyan
$xamlFiles = Get-ChildItem -Path $features, $shell -Recurse -Filter "*.xaml" -File | Where-Object { -not (Should-Exclude $_.FullName) }
if ($xamlFiles.Count -eq 0) { Write-Host "No se encontraron archivos XAML." -ForegroundColor Yellow; exit 2 }
$totalHardcoded = 0; $filesWithIssues = @()
foreach ($file in $xamlFiles) {
    $relPath = $file.FullName.Substring($root.Length + 1)
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $matches = Get-HexColorRefs $content
    if ($matches.Count -gt 0) {
        $totalHardcoded += $matches.Count
        $filesWithIssues += @{Path=$relPath; Count=$matches.Count; Samples=($matches | Select-Object -First 5).Value -join ', '}
    }
}
Write-Host "Archivos XAML examinados: $($xamlFiles.Count)"
Write-Host "Archivos con colores hardcodeados: $($filesWithIssues.Count)"
Write-Host "Total colores hardcodeados: $totalHardcoded"
if ($filesWithIssues.Count -gt 0) {
    Write-Host "Archivos con colores hardcodeados:" -ForegroundColor Yellow
    foreach ($f in $filesWithIssues) {
        $severity = if ($f.Count -gt 10) { "ALTO" } elseif ($f.Count -gt 5) { "MEDIO" } else { "BAJO" }
        if ($ReportOnly) { Write-Host "  [$severity] $($f.Path) - $($f.Count) colores (ej: $($f.Samples))" }
        else { Write-Host "::warning file=$($f.Path),line=1::$($f.Count) colores hardcodeados" }
    }
    if (-not $ReportOnly) { Write-Host "Algunos colores hardcodeados son intencionales (accent de graficos, semaforos). La meta es que fondos/superficies/canvas usen recursos del DesignSystem." -ForegroundColor Yellow }
    exit 1
}
Write-Host "Modo oscuro: cobertura completa." -ForegroundColor Green
exit 0
