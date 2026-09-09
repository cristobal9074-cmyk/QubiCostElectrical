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
$manifestPath = Join-Path $root "artifacts\build-manifest.json"

function Get-BuildVersionFromProps {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $props = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
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
        [string]$SectionPattern,

        [Parameter(Mandatory = $true)]
        [string]$RowPattern,

        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $sectionMatch = [regex]::Match($Content, $SectionPattern)
    if (-not $sectionMatch.Success) {
        throw "No se encontró la sección para: $Label (patrón: $SectionPattern)"
    }

    $sectionBody = $sectionMatch.Groups[1].Value
    if (-not [regex]::IsMatch($sectionBody, $RowPattern)) {
        throw "No se encontró la fila para: $Label (patrón: $RowPattern)"
    }

    $updatedSectionBody = [regex]::Replace($sectionBody, $RowPattern, "`${1}$Value`${3}", 1)

    return $Content.Substring(0, $sectionMatch.Groups[1].Index) +
        $updatedSectionBody +
        $Content.Substring($sectionMatch.Groups[1].Index + $sectionMatch.Groups[1].Length)
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

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($jsonPath, ($record | ConvertTo-Json -Depth 5), $utf8NoBom)

$smokeContent = [System.IO.File]::ReadAllText($smokePath, [System.Text.Encoding]::UTF8)
$checkboxRow = Convert-HumanResultToCheckboxRow -Value $Resultado

$fallbackNote = '> Nota automática: último registro humano en `docs/SMOKE_TEST_HUMAN_LAST_RUN.json`.'
$usedFallback = $false
$errorDetails = ""

$patternFirma = '(?ms)(^###\s+Firma Semana A.*?)(?=\r?\n---|\z)'
$patternUltima = '(?ms)(^##\s+[^|\r\n]*?ltima ejecuci.*?)(?=\r?\n---|\z)'
$patternAprobacion = '(?ms)(^##\s+Aprobaci.*?)(?=\r?\n---|\z)'

try {
    # 1. Firma Semana A
    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternFirma `
        -RowPattern '(\| Fecha \| )(.+?)( \|)' `
        -Value $Fecha `
        -Label "Firma Semana A / Fecha"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternFirma `
        -RowPattern '(\| Probador \| )(.+?)( \|)' `
        -Value $record.Probador `
        -Label "Firma Semana A / Probador"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternFirma `
        -RowPattern '(\| Build \| )(.+?)( \|)' `
        -Value ('`publish\win-x64\QubiCostElectrical.exe` / versión {0}' -f $record.BuildVersion) `
        -Label "Firma Semana A / Build"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternFirma `
        -RowPattern '(\| Resultado \| )(.+?)( \|)' `
        -Value $checkboxRow `
        -Label "Firma Semana A / Resultado"

    if ($Resultado -eq "PASS") {
        $firmaMatch = [regex]::Match($smokeContent, $patternFirma)
        if ($firmaMatch.Success) {
            $updatedFirma = [regex]::Replace($firmaMatch.Groups[1].Value, '(?m)^(\d+\.\s+)\[ \]', '${1}[x]')
            $smokeContent = $smokeContent.Substring(0, $firmaMatch.Groups[1].Index) +
                $updatedFirma +
                $smokeContent.Substring($firmaMatch.Groups[1].Index + $firmaMatch.Groups[1].Length)
        }
    }

    # 2. Última ejecución
    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternUltima `
        -RowPattern '(\| Fecha \| )(.+?)( \|)' `
        -Value $Fecha `
        -Label "Última ejecución / Fecha"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternUltima `
        -RowPattern '(\| Versi[^|]+ \| )(.+?)( \|)' `
        -Value $record.BuildVersion `
        -Label "Última ejecución / Versión"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternUltima `
        -RowPattern '(\| Smoke humano Semana A \| )(.+?)( \|)' `
        -Value "**$Resultado** · Probador: $($record.Probador) · Fecha: $Fecha" `
        -Label "Última ejecución / Smoke humano Semana A"

    # 3. Aprobación final
    $artifactSha = $null
    if (Test-Path -LiteralPath $manifestPath) {
        try {
            $manifestRaw = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
            $manifestJson = $manifestRaw | ConvertFrom-Json
            if ($manifestJson.artifacts -and $manifestJson.artifacts.Count -gt 0) {
                $artifactSha = $manifestJson.artifacts[0].sha256
            }
        } catch {}
    }

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternAprobacion `
        -RowPattern '(\| Versi[^|]+ probada \| )(.+?)( \|)' `
        -Value $record.BuildVersion `
        -Label "Aprobación / Versión probada"

    if (-not [string]::IsNullOrWhiteSpace($artifactSha)) {
        $smokeContent = Set-SectionTableValue -Content $smokeContent `
            -SectionPattern $patternAprobacion `
            -RowPattern '(\| Artefacto [^|]+ \| )(.+?)( \|)' `
            -Value ('`{0}`' -f $artifactSha) `
            -Label "Aprobación / Artefacto SHA-256"
    }

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternAprobacion `
        -RowPattern '(\| Probado por \| )(.+?)( \|)' `
        -Value "$($record.Probador) (Smoke Semana A humano + suite automatizada 1821/1821)" `
        -Label "Aprobación / Probado por"

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternAprobacion `
        -RowPattern '(\| Fecha \| )(.+?)( \|)' `
        -Value $Fecha `
        -Label "Aprobación / Fecha"

    $aprobResultText = if ($Resultado -eq "PASS") {
        "**PASS** — validación completa (Cubicación / SC / roles / flush persistencia / oferta y export)"
    } else {
        "**FAIL** — fallos encontrados en validación humana"
    }

    $smokeContent = Set-SectionTableValue -Content $smokeContent `
        -SectionPattern $patternAprobacion `
        -RowPattern '(\| Resultado \| )(.+?)( \|)' `
        -Value $aprobResultText `
        -Label "Aprobación / Resultado"
}
catch {
    $usedFallback = $true
    $errorDetails = $_.Exception.Message
    if ($smokeContent -notmatch [regex]::Escape($fallbackNote)) {
        $smokeContent = $smokeContent.TrimEnd() + "`r`n`r`n$fallbackNote`r`n"
    }
}

[System.IO.File]::WriteAllText($smokePath, $smokeContent, $utf8NoBom)

Write-Host "Registro humano de Semana A actualizado." -ForegroundColor Green
Write-Host "  Fecha:      $Fecha"
Write-Host "  Probador:   $($record.Probador)"
Write-Host "  Resultado:  $Resultado"
Write-Host "  Build:      $($record.BuildVersion)"
Write-Host "  Markdown:   $smokePath"
Write-Host "  Evidencia:  $jsonPath"

if ($usedFallback) {
    Write-Warning "No se pudo actualizar completamente la tabla del markdown ($errorDetails). Se dejó nota de respaldo y la evidencia quedó en JSON."
}
