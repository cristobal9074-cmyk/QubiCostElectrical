$src = 'C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL039A2026 ABIERTO.qce'
$destDir = Join-Path $PSScriptRoot 'corpus'
$dest = Join-Path $destDir 'PEL039A2026-ABIERTO.qce'
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
Copy-Item -LiteralPath $src -Destination $dest -Force
$item = Get-Item -LiteralPath $dest
Write-Output ("COPIED " + $item.Length)
