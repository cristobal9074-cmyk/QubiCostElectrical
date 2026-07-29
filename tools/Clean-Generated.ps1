param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$excludedDirectoryNames = @("bin", "obj", ".vs", "TestResults", "artifacts", "publish")
$generatedFilePatterns = @("*.dll", "*.exe", "*.pdb", "*.cache")

function Assert-InWorkspace {
    param([string]$Path)
    $resolved = Resolve-Path -LiteralPath $Path
    if (-not $resolved.Path.StartsWith($root.Path, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside workspace: $($resolved.Path)"
    }
    return $resolved.Path
}

$directories = Get-ChildItem -LiteralPath $root.Path -Recurse -Force -Directory |
    Where-Object { $excludedDirectoryNames -contains $_.Name } |
    Sort-Object FullName -Descending

foreach ($directory in $directories) {
    $path = Assert-InWorkspace $directory.FullName
    if ($WhatIf) {
        Write-Host "Would remove directory: $path"
    } else {
        Remove-Item -LiteralPath $path -Recurse -Force
        Write-Host "Removed directory: $path"
    }
}

foreach ($pattern in $generatedFilePatterns) {
    $files = Get-ChildItem -LiteralPath $root.Path -Recurse -Force -File -Filter $pattern |
        Where-Object { $_.FullName -notmatch "\\.git\\" }

    foreach ($file in $files) {
        $path = Assert-InWorkspace $file.FullName
        if ($WhatIf) {
            Write-Host "Would remove file: $path"
        } else {
            Remove-Item -LiteralPath $path -Force
            Write-Host "Removed file: $path"
        }
    }
}
