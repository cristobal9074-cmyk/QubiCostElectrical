param(
    [string[]]$Paths,
    [switch]$Strict
)

$ErrorActionPreference = "Stop"

function Exit-WithCode {
    param(
        [int]$Code,
        [string]$Message,
        [ConsoleColor]$Color = [ConsoleColor]::Yellow
    )

    if ($Message) {
        Write-Host $Message -ForegroundColor $Color
    }

    exit $Code
}

function Get-SignToolPath {
    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($command -and $command.Source) {
        return $command.Source
    }

    $windowsKitsRoots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $windowsKitsRoots) {
        $candidate = Get-ChildItem -Path $root -Filter "signtool.exe" -File -Recurse -ErrorAction SilentlyContinue |
            Sort-Object -Property FullName -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    return $null
}

function Resolve-SigningTargets {
    param(
        [string[]]$InputPaths
    )

    $resolved = New-Object System.Collections.Generic.List[string]

    foreach ($inputPath in $InputPaths) {
        if ([string]::IsNullOrWhiteSpace($inputPath)) {
            continue
        }

        $hasWildcard = $inputPath.IndexOfAny(@([char]'*', [char]'?')) -ge 0
        if ($hasWildcard) {
            $matches = Get-ChildItem -Path $inputPath -File -ErrorAction SilentlyContinue
            foreach ($match in $matches) {
                $resolved.Add($match.FullName)
            }

            continue
        }

        if (-not (Test-Path -LiteralPath $inputPath)) {
            throw "Ruta no encontrada: $inputPath"
        }

        $item = Get-Item -LiteralPath $inputPath -ErrorAction Stop
        if ($item.PSIsContainer) {
            throw "Se esperaba un archivo, pero se recibió un directorio: $inputPath"
        }

        $resolved.Add($item.FullName)
    }

    return $resolved | Select-Object -Unique
}

if (-not $Paths -or $Paths.Count -eq 0) {
    Exit-WithCode -Code 0 -Message "No se recibieron rutas para firmar." -Color Green
}

$certPath = $env:CODE_SIGNING_CERT_PFX
$certPassword = $env:CODE_SIGNING_CERT_PASSWORD

if ([string]::IsNullOrWhiteSpace($certPath) -or [string]::IsNullOrWhiteSpace($certPassword)) {
    $message = "Firma omitida: faltan CODE_SIGNING_CERT_PFX y/o CODE_SIGNING_CERT_PASSWORD."
    if ($Strict) {
        Exit-WithCode -Code 1 -Message "$message Strict activo." -Color Red
    }

    Exit-WithCode -Code 0 -Message "$message RC interna permitida." -Color Yellow
}

if (-not (Test-Path -LiteralPath $certPath)) {
    Exit-WithCode -Code 1 -Message "Certificado no encontrado en CODE_SIGNING_CERT_PFX: $certPath" -Color Red
}

$signTool = Get-SignToolPath
if (-not $signTool) {
    Exit-WithCode -Code 1 -Message "signtool.exe no fue encontrado en PATH ni en Windows Kits 10." -Color Red
}

$targets = Resolve-SigningTargets -InputPaths $Paths
if (-not $targets -or $targets.Count -eq 0) {
    Exit-WithCode -Code 0 -Message "No se encontraron archivos coincidentes para firmar." -Color Green
}

Write-Host "Usando signtool: $signTool" -ForegroundColor Cyan
Write-Host "Archivos a firmar: $($targets.Count)" -ForegroundColor Cyan

foreach ($target in $targets) {
    Write-Host "Firmando $target" -ForegroundColor Yellow

    & $signTool sign `
        /fd SHA256 `
        /td SHA256 `
        /tr "http://timestamp.digicert.com" `
        /f $certPath `
        /p $certPassword `
        $target

    if ($LASTEXITCODE -ne 0) {
        Exit-WithCode -Code 1 -Message "La firma falló para: $target" -Color Red
    }
}

Exit-WithCode -Code 0 -Message "Firma Authenticode completada." -Color Green
