<#
.SYNOPSIS
    Instala el certificado público de Qubi Cost Electrical en la máquina del cliente.

.DESCRIPTION
    Agrega el archivo .CER a las entidades de certificación raíz de confianza y a
    editores de confianza para que Windows reconozca las firmas digitales de la aplicación.
#>
param(
    [string]$CerPath
)

$ErrorActionPreference = "Stop"

if (-not $CerPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $candidates = @(
        (Join-Path $scriptDir "..\certs\QubiCostElectrical-Public.cer"),
        (Join-Path $scriptDir "QubiCostElectrical-Public.cer"),
        "QubiCostElectrical-Public.cer"
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $CerPath = Resolve-Path $cand
            break
        }
    }
}

if (-not $CerPath -or -not (Test-Path $CerPath)) {
    Write-Host "Error: No se encontró el archivo de certificado público (.cer)." -ForegroundColor Red
    exit 1
}

Write-Host "Instalando certificado de confianza de Qubi Cost Electrical..." -ForegroundColor Cyan
Write-Host "Archivo: $CerPath" -ForegroundColor DarkGray

$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CerPath)

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$storeLocation = if ($isAdmin) {
    [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
} else {
    [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
}

Write-Host "Ubicación de instalación: $storeLocation" -ForegroundColor Yellow

# 1. Agregar a Root
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
    [System.Security.Cryptography.X509Certificates.StoreName]::Root,
    $storeLocation
)
$rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$rootStore.Add($cert)
$rootStore.Close()
Write-Host "  OK Instalado en Entidades de certificación raíz de confianza." -ForegroundColor Green

# 2. Agregar a TrustedPublisher
$pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
    [System.Security.Cryptography.X509Certificates.StoreName]::TrustedPublisher,
    $storeLocation
)
$pubStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$pubStore.Add($cert)
$pubStore.Close()
Write-Host "  OK Instalado en Editores de confianza." -ForegroundColor Green

Write-Host "`n¡Certificado instalado exitosamente!" -ForegroundColor Green
Write-Host "Ahora los ejecutables de Qubi Cost Electrical se abrirán como software de confianza." -ForegroundColor White
