#Requires -Version 5.1
<#
.SYNOPSIS
    Registra la firma humana del smoke Semana A sin editar manualmente el markdown.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Fecha,

    [Parameter(Mandatory = $true)]
    [string]$Probador,

    [Parameter(Mandatory = $true)]
    [ValidateSet("PASS", "FAIL")]
    [string]$Resultado,

    [string]$BuildVersion
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$propsPath = Join-Path $root "Directory.Build.props"
$smokePath = Join-Path $root "docs\SMOKE_TEST.md"
$jsonPath = Join-Path $root "docs\SMOKE_TEST_HUMAN_LAST_RUN.json"

function Get-BuildVersionFromProps {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $props = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $informational = [regex]::Match($props, '<InformationalVersion>([^<]+)</InformationalVersion>').Groups[1].Value.Trim()
    if (-not [string]::IsNullOrWhiteSpace($informational)) {
        return $informational
    }

    $prefix = [regex]::Match($props, '<VersionPrefix>([^<]+)</VersionPrefix>').Groups[1].Value.Trim()
    $suffix = [regex]::Match($props, '<VersionSuffix>([^<]+)</VersionSuffix>').Groups[1].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($prefix)) {
        throw "No se pudo leer InformationalVersion ni VersionPrefix desde Directory.Build.props."
    }

    if (-not [string]::IsNullOrWhiteSpace($suffix)) {
        return "$prefix-$suffix"
    }

    return $prefix
}

function Convert-HumanResultToCheckboxRow {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("PASS", "FAIL")]
        [string]$Value
    )

    if ($Value -eq "PASS") {
        return "[x] PASS · [ ] FAIL"
    }

    return "[ ] PASS · [x] FAIL"
}

function Set-SectionTableValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$SectionTitle,

        [Parameter(Mandatory = $true)]
        [string]$RowLabel,

        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $sectionPattern = "(?ms)(^###+\s+{0}.*?)(\r?\n---)" -f [regex]::Escape($SectionTitle)
    $sectionMatch = [regex]::Match($Content, $sectionPattern)
    if (-not $sectionMatch.Success) {
        throw "No se encontró la sección: $SectionTitle"
    }

    $sectionBody = $sectionMatch.Groups[1].Value
    $rowPattern = "(\| {0} \| )(.+?)( \|)" -f [regex]::Escape($RowLabel)
    $updatedSectionBody = [regex]::Replace($sectionBody, $rowPattern, "`${1}$Value`${3}", 1)
    if ($updatedSectionBody -eq $sectionBody) {
        throw "No se pudo actualizar: $Label"
    }

    return $Content.Substring(0, $sectionMatch.Groups[1].Index) +
        $updatedSectionBody +
        $Content.Substring($sectionMatch.Groups[2].Index)
}

[void][DateTime]::ParseExact($Fecha, "yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture)

if ([string]::IsNullOrWhiteSpace($Probador)) {
    throw "Probador no puede estar vacío."
}

if ([string]::IsNullOrWhiteSpace($BuildVersion)) {
    $BuildVersion = Get-BuildVersionFromProps -Path $propsPath
}

$record = [ordered]@{
    Fecha = $Fecha
    Probador = $Probador.Trim()
    Resultado = $Resultado
    BuildVersion = $BuildVersion.Trim()
    RegistradoEn = (Get-Date).ToString("s")
    Fuente = "scripts/Record-SmokeSemanaA.ps1"
}

$record | ConvertTo-Json | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$smokeContent = Get-Content -LiteralPath $smokePath -Raw -Encoding UTF8
$checkboxRow = Convert-HumanResultToCheckboxRow -Value $Resultado

$fallbackNote = "> Nota automática: último registro humano en `docs/SMOKE_TEST_HUMAN_LAST_RUN.json`."
$usedFallback = $false

try {
    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionTitle "Firma Semana A (humano obligatorio)" `
        -RowLabel "Fecha" `
        -Value $Fecha `
        -Label "Firma Semana A / Fecha"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionTitle "Firma Semana A (humano obligatorio)" `
        -RowLabel "Probador" `
        -Value $record.Probador `
        -Label "Firma Semana A / Probador"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionTitle "Firma Semana A (humano obligatorio)" `
        -RowLabel "Build" `
        -Value "`publish\win-x64\QubiCostElectrical.exe` / versión $($record.BuildVersion)" `
        -Label "Firma Semana A / Build"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionTitle "Firma Semana A (humano obligatorio)" `
        -RowLabel "Resultado" `
        -Value $checkboxRow `
        -Label "Firma Semana A / Resultado"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionTitle "Última ejecución" `
        -RowLabel "Fecha" `
        -Value $Fecha `
        -Label "Última ejecución / Fecha"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionTitle "Última ejecución" `
        -RowLabel "Versión" `
        -Value $record.BuildVersion `
        -Label "Última ejecución / Versión"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionTitle "Última ejecución" `
        -RowLabel "Smoke humano Semana A" `
        -Value "**$Resultado** · Probador: $($record.Probador) · Fecha: $Fecha" `
        -Label "Última ejecución / Smoke humano Semana A"
}
catch {
    $usedFallback = $true
    if ($smokeContent -notmatch [regex]::Escape($fallbackNote)) {
        $smokeContent = $smokeContent.TrimEnd() + "`r`n`r`n$fallbackNote`r`n"
    }
}

Set-Content -LiteralPath $smokePath -Value $smokeContent -Encoding UTF8

Write-Host "Registro humano de Semana A actualizado." -ForegroundColor Green
Write-Host "  Fecha:      $Fecha"
Write-Host "  Probador:   $($record.Probador)"
Write-Host "  Resultado:  $Resultado"
Write-Host "  Build:      $($record.BuildVersion)"
Write-Host "  Markdown:   $smokePath"
Write-Host "  Evidencia:  $jsonPath"

if ($usedFallback) {
    Write-Warning "No se pudo actualizar completamente la tabla del markdown. Se dejó nota de respaldo y la evidencia quedó en JSON."
}
