#Requires -Version 5.1
<#
.SYNOPSIS
    Firma binarios Windows con Authenticode (signtool).

.DESCRIPTION
    Requiere variables de entorno o parámetros:
      CODE_SIGNING_CERT_PFX   — ruta al .pfx (o contenido base64 si -CertBase64)
      CODE_SIGNING_CERT_PASSWORD — contraseña del certificado
      CODE_SIGNING_TIMESTAMP_URL — opcional; default http://timestamp.digicert.com

    Sin certificado configurado, el script termina con código 2 (omitido).
    Con certificado inválido o signtool ausente, termina con código 1 (error).

.EXAMPLE
    $env:CODE_SIGNING_CERT_PFX = "C:\certs\qubicost.pfx"
    $env:CODE_SIGNING_CERT_PASSWORD = "secret"
    .\deploy\Sign-Authenticode.ps1 -Paths deploy\releases\*.exe, deploy\releases\*.nupkg
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Paths,

    [string]$CertPath = $env:CODE_SIGNING_CERT_PFX,
    [string]$CertPassword = $env:CODE_SIGNING_CERT_PASSWORD,
    [string]$TimestampUrl = $(if ($env:CODE_SIGNING_TIMESTAMP_URL) { $env:CODE_SIGNING_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }),
    [switch]$CertBase64
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($CertPath)) {
    Write-Host "Sign-Authenticode: sin certificado (CODE_SIGNING_CERT_PFX). Firma omitida." -ForegroundColor Yellow
    exit 2
}

if ([string]::IsNullOrWhiteSpace($CertPassword)) {
    throw "Se requiere CODE_SIGNING_CERT_PASSWORD para firmar."
}

function Resolve-SignTool {
    $sdk = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $sdk) {
        $latest = Get-ChildItem $sdk -Directory | Sort-Object Name -Descending | Select-Object -First 1
        $candidate = Join-Path $latest.FullName "x64\signtool.exe"
        if (Test-Path $candidate) { return $candidate }
    }

    $onPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    throw "signtool.exe no encontrado. Instala Windows SDK o Visual Studio Build Tools."
}

$signTool = Resolve-SignTool
$tempPfx = $null

try {
    if ($CertBase64) {
        $tempPfx = Join-Path $env:TEMP "codesign-$(New-Guid).pfx"
        [IO.File]::WriteAllBytes($tempPfx, [Convert]::FromBase64String($CertPath))
        $CertPath = $tempPfx
    }

    if (-not (Test-Path -LiteralPath $CertPath)) {
        throw "Certificado no encontrado: $CertPath"
    }

    $files = foreach ($pattern in $Paths) {
        Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue
    } | Select-Object -Unique

    if (-not $files -or $files.Count -eq 0) {
        Write-Host "Sign-Authenticode: no hay archivos que firmar." -ForegroundColor Yellow
        exit 2
    }

    foreach ($file in $files) {
        Write-Host "Firmando $($file.FullName)..." -ForegroundColor Cyan
        & $signTool sign `
            /f $CertPath `
            /p $CertPassword `
            /tr $TimestampUrl `
            /td sha256 `
            /fd sha256 `
            /a `
            $file.FullName

        if ($LASTEXITCODE -ne 0) {
            throw "signtool falló para $($file.Name) (código $LASTEXITCODE)"
        }

        & $signTool verify /pa /v $file.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "Verificación de firma falló para $($file.Name)"
        }
        Write-Host "  OK $($file.Name)" -ForegroundColor Green
    }
}
finally {
    if ($tempPfx -and (Test-Path -LiteralPath $tempPfx)) {
        Remove-Item -LiteralPath $tempPfx -Force
    }
}

Write-Host "Sign-Authenticode: $($files.Count) archivo(s) firmado(s)." -ForegroundColor Green
exit 0
