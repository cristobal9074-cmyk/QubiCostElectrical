param(
    [switch]$Sign
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishScript = Join-Path $root "scripts\Publish-Release.ps1"

if (-not (Test-Path -LiteralPath $publishScript)) {
    throw "No se encontró el script esperado: $publishScript"
}

$arguments = @{
    Configuration = "Release"
    Runtime       = "win-x64"
}

if ($Sign) {
    $arguments.Sign = $true
}

& $publishScript @arguments
exit $LASTEXITCODE
