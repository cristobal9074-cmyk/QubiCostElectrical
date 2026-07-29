param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

# No crear paquetes desde código con cambios sin confirmar.
$gitStatus = git -C $root.Path status --porcelain

if ($LASTEXITCODE -ne 0) {
    throw "No fue posible comprobar el estado del repositorio Git."
}

if (-not [string]::IsNullOrWhiteSpace(($gitStatus -join "`n"))) {
    Write-Host ""
    Write-Host "Cambios pendientes encontrados:" -ForegroundColor Yellow
    $gitStatus | ForEach-Object { Write-Host $_ }
    Write-Host ""

    throw "No se puede crear el ZIP: primero confirma o descarta los cambios pendientes."
}

$branch = (git -C $root.Path branch --show-current).Trim()
$allowedBranches = @("master", "main")
if ($allowedBranches -notcontains $branch) {
    throw "No se puede crear el ZIP desde la rama '$branch'. Usa master o main."
}

# Lee la versión desde Directory.Build.props (fuente única de verdad).
$propsFile = Join-Path $root.Path "Directory.Build.props"
if (-not (Test-Path $propsFile)) { throw "No se encontró Directory.Build.props" }

$propsContent = Get-Content -LiteralPath $propsFile -Raw
$versionMatch = [regex]::Match($propsContent, '<VersionPrefix>([^<]+)</VersionPrefix>')
if (-not $versionMatch.Success) { throw "No se encontró VersionPrefix en Directory.Build.props" }
$version = $versionMatch.Groups[1].Value
Write-Host "Versión detectada: $version"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $outputDirectory = Join-Path $root.Path "artifacts"
    $OutputPath = Join-Path $outputDirectory "QubiCostElectrical-source-$version.zip"
} else {
    $outputDirectory = Split-Path -Parent $OutputPath
    if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
        $outputDirectory = $root.Path
        $OutputPath = Join-Path $outputDirectory $OutputPath
    }
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$excludeDirectoryNames = @(
    ".git",
    ".claude",
    ".cursor",
    ".vs",
    "worktrees",
    "bin",
    "obj",
    "TestResults",
    "artifacts",
    "publish"
)
$excludeFileExtensions  = @(".dll", ".exe", ".pdb", ".ilk", ".exp", ".lib", ".cache", ".zip")
$excludeFileNames       = @("Thumbs.db", ".DS_Store", "desktop.ini", "company.iss")

# Excluir imágenes de la raíz y archivos temporales
$excludeRootImages = $true

$files = Get-ChildItem -LiteralPath $root.Path -Recurse -Force -File | Where-Object {
    $relative = $_.FullName.Substring($root.Path.Length).TrimStart("\", "/")
    $parts    = $relative -split "[\\/]"

    # Skip directorio excluido en cualquier nivel del path
    if ($parts | Where-Object { $excludeDirectoryNames -contains $_ }) { return $false }

    # Skip extensiones excluidas
    if ($excludeFileExtensions -contains $_.Extension.ToLowerInvariant()) { return $false }

    # Skip nombres excluidos
    if ($excludeFileNames -contains $_.Name) { return $false }

    # Skip imágenes sueltas en la raíz (screenshots, capturas debug)
    if ($excludeRootImages -and $parts.Count -eq 1) {
        $imgExts = @(".png", ".jpg", ".jpeg", ".gif", ".bmp")
        if ($imgExts -contains $_.Extension.ToLowerInvariant()) { return $false }
    }

    return $true
}

if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Force
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("qce-source-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    foreach ($file in $files) {
        $relative       = $file.FullName.Substring($root.Path.Length).TrimStart("\", "/")
        $target         = Join-Path $tempRoot $relative
        $targetDirectory = Split-Path -Parent $target
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }

    Compress-Archive -Path (Join-Path $tempRoot "*") -DestinationPath $OutputPath -Force

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($OutputPath)
    try {
        $forbidden = $zip.Entries | Where-Object {
            $normalized = $_.FullName.Replace("\", "/")
            $normalized -match '(^|/)\.git(/|$)' -or
            $normalized -match '(^|/)\.claude(/|$)' -or
            $normalized -match '(^|/)worktrees(/|$)'
        }
        if ($forbidden) {
            $sample = ($forbidden | Select-Object -First 5 | ForEach-Object { $_.FullName }) -join ", "
            throw "El ZIP contiene rutas prohibidas: $sample"
        }
    } finally {
        $zip.Dispose()
    }

    $sizeBytes = (Get-Item $OutputPath).Length
    $sizeKb = [math]::Round($sizeBytes / 1KB, 0)
    $sizeMb = [math]::Round($sizeBytes / 1MB, 2)
    $sizeLabel = if ($sizeMb -lt 1) { "$sizeKb KB" } else { "$sizeMb MB" }
    Write-Host "ZIP fuente creado: $OutputPath ($sizeLabel, $($files.Count) archivos)"
} finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
