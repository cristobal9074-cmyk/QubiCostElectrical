param(
    [switch]$Build,
    [string]$Configuration = "Release",
    [string]$ShortcutPath = "$env:USERPROFILE\Desktop\Qubi Cost Electrical.lnk"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
# RuntimeIdentifier=win-x64 fijo en el .csproj (evita que dotnet build arrastre binarios
# nativos de Linux/Mac) mueve la salida a una subcarpeta win-x64\ dentro de net10.0-windows\.
$exe = Join-Path $root "src\QubiCostElectrical.Wpf\bin\$Configuration\net10.0-windows\win-x64\QubiCostElectrical.exe"
$solution = Join-Path $root "QubiCostElectrical.Wpf.sln"
$propsPath = Join-Path $root "Directory.Build.props"

if ($Build) {
    dotnet build $solution -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build falló" }
}

if (-not (Test-Path -LiteralPath $exe)) {
    throw "No existe el ejecutable. Compile primero: dotnet build $solution -c $Configuration`n  $exe"
}

$props = Get-Content -LiteralPath $propsPath -Raw
$version = [regex]::Match($props, '<InformationalVersion>([^<]+)</InformationalVersion>').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($version)) {
    $prefix = [regex]::Match($props, '<VersionPrefix>([^<]+)</VersionPrefix>').Groups[1].Value
    $suffix = [regex]::Match($props, '<VersionSuffix>([^<]+)</VersionSuffix>').Groups[1].Value
    $version = if ($suffix) { "$prefix-$suffix" } else { $prefix }
}

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($ShortcutPath)
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $root
$lnk.Arguments = ""
$lnk.IconLocation = "$exe,0"
$lnk.Description = "Qubi Cost Electrical v$version"
$lnk.Save()

$built = (Get-Item -LiteralPath $exe).LastWriteTime
Write-Host "Acceso directo actualizado: $ShortcutPath"
Write-Host "  Ejecutable: $exe"
Write-Host "  Versión:    v$version"
Write-Host "  Compilado:  $built"
