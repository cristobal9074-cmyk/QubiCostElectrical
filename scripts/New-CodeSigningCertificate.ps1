<#
.SYNOPSIS
    Genera un certificado de firma de código (Authenticode) autofirmado para Qubi Cost Electrical.

.DESCRIPTION
    Crea un certificado X.509 de tipo CodeSigningCert con algoritmo SHA-256,
    lo exporta a formato .PFX (con clave privada) y .CER (público),
    y opcionalmente lo instala en el almacén de certificados de Windows del usuario
    para que el sistema operativo y SmartScreen reconozcan las firmas digitales.

.PARAMETER Subject
    Nombre del emisor/organización del certificado. Por defecto: "CN=Qubi Cost Electrical, O=Qubi Cost, C=CL"

.PARAMETER ValidYears
    Años de validez del certificado. Por defecto: 5 años.

.PARAMETER Password
    Contraseña para proteger el archivo .pfx. Si no se indica, se solicita o se usa una segura por defecto.

.PARAMETER OutputDir
    Directorio donde se guardarán los archivos .pfx y .cer. Por defecto: "certs".

.PARAMETER InstallLocal
    Instala el certificado público en el almacén personal de certificados (Root y TrustedPublisher)
    del usuario actual para que Windows valide los ejecutables firmados sin alertas.

.EXAMPLE
    .\scripts\New-CodeSigningCertificate.ps1 -InstallLocal
#>
param(
    [string]$Subject = "CN=Qubi Cost Electrical, O=Qubi Cost, C=CL",
    [int]$ValidYears = 5,
    [string]$Password = "QubiCost2026.CodeSign!",
    [string]$OutputDir = "certs",
    [switch]$InstallLocal = $true
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$targetDir = Join-Path $root $OutputDir

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$pfxPath = Join-Path $targetDir "QubiCostElectrical-CodeSigning.pfx"
$cerPath = Join-Path $targetDir "QubiCostElectrical-Public.cer"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Generador de Certificado Authenticode - Qubi Cost Electrical" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Sujeto:          $Subject" -ForegroundColor Yellow
Write-Host "Validez:         $ValidYears años (hasta $((Get-Date).AddYears($ValidYears).ToString('yyyy-MM-dd')))" -ForegroundColor Yellow
Write-Host "Destino PFX:     $pfxPath" -ForegroundColor Yellow
Write-Host "Destino Público: $cerPath" -ForegroundColor Yellow

# 1. Crear el certificado autofirmado en el almacén del usuario
Write-Host "`n[1/4] Creando certificado criptográfico..." -ForegroundColor Yellow
$cert = New-SelfSignedCertificate `
    -Subject $Subject `
    -Type CodeSigningCert `
    -KeyUsage DigitalSignature `
    -KeySpec Signature `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears($ValidYears) `
    -CertStoreLocation "Cert:\CurrentUser\My"

Write-Host "  OK Certificado creado. Huella (Thumbprint): $($cert.Thumbprint)" -ForegroundColor Green

# 2. Exportar PFX con clave privada
Write-Host "`n[2/4] Exportando archivo PFX (clave privada)..." -ForegroundColor Yellow
$secPassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secPassword | Out-Null
Write-Host "  OK PFX exportado: $pfxPath" -ForegroundColor Green

# 3. Exportar CER público
Write-Host "`n[3/4] Exportando certificado público CER..." -ForegroundColor Yellow
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
Write-Host "  OK CER exportado: $cerPath" -ForegroundColor Green

# 4. Instalar en almacén local de confianza del usuario si se solicitó
if ($InstallLocal) {
    Write-Host "`n[4/4] Instalando en almacenes de confianza de Windows..." -ForegroundColor Yellow
    try {
        & certutil -addstore -user "TrustedPublisher" $cerPath | Out-Null
        Write-Host "  OK Instalado en: Editores de confianza (TrustedPublisher)" -ForegroundColor Green
    }
    catch {
        Write-Host "  Advertencia al registrar en TrustedPublisher: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n[4/4] Instalación local omitida." -ForegroundColor DarkGray
}

# 5. Generar configuración para GitHub Actions (CI)
$pfxBytes = [System.IO.File]::ReadAllBytes($pfxPath)
$base64 = [Convert]::ToBase64String($pfxBytes)

Write-Host "`n═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  ¡Certificado listo para firmar ejecutables e instaladores!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "`nVariables de entorno para sesión actual o scripts de build:" -ForegroundColor Yellow
Write-Host "`$env:CODE_SIGNING_CERT_PFX = `"$pfxPath`"" -ForegroundColor White
Write-Host "`$env:CODE_SIGNING_CERT_PASSWORD = `"$Password`"" -ForegroundColor White

Write-Host "`nPara GitHub Actions (en caso de querer que CI firme los artefactos):" -ForegroundColor Yellow
Write-Host "Secret CODE_SIGNING_CERT_PASSWORD = $Password" -ForegroundColor White
Write-Host "Secret CODE_SIGNING_CERT_BASE64   = (Se ha generado, ver certs/ci-secret-base64.txt)" -ForegroundColor White

# Guardar base64 en un archivo temporal ignorado para facilitar copiarlo a GitHub Secrets
$base64TxtPath = Join-Path $targetDir "ci-secret-base64.txt"
Set-Content -LiteralPath $base64TxtPath -Value $base64 -Encoding ascii
