param(
    # Compila/publica antes de crear el acceso directo (opcional).
    [switch]$Build,

    # Fuerza la ruta de desarrollo: bin\Release\...\win-x64\ (escape hatch).
    [switch]$UseBin,

    # Usa publish\win-x64\ (comportamiento por defecto; el switch documenta la intención).
    [switch]$UsePublish,

    # Registra en HKCU la asociación .qce → QubiCostElectrical.Project al mismo exe.
    [switch]$RegisterAssociation,

    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$ShortcutPath = "$env:USERPROFILE\Desktop\Qubi Cost Electrical.lnk"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solution = Join-Path $root "QubiCostElectrical.Wpf.sln"
$project = Join-Path $root "src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj"
$propsPath = Join-Path $root "Directory.Build.props"

# Preferir publish salvo -UseBin explícito.
$preferPublish = -not $UseBin
if ($UsePublish -and $UseBin) {
    throw "No combines -UsePublish y -UseBin."
}

$publishDir = Join-Path $root "publish\$Runtime"
$binExe = Join-Path $root "src\QubiCostElectrical.Wpf\bin\$Configuration\net10.0-windows\$Runtime\QubiCostElectrical.exe"
$publishExe = Join-Path $publishDir "QubiCostElectrical.exe"
$exe = if ($preferPublish) { $publishExe } else { $binExe }

if ($Build) {
    if ($preferPublish) {
        if (-not (Test-Path -LiteralPath $publishDir)) {
            New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
        }
        dotnet publish $project `
            --configuration $Configuration `
            --runtime $Runtime `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:EnableCompressionInSingleFile=true `
            -p:DebugType=none `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            --output $publishDir
        if ($LASTEXITCODE -ne 0) { throw "Publish falló" }
    }
    else {
        # RuntimeIdentifier=win-x64 fijo en el .csproj mueve la salida a win-x64\.
        dotnet build $solution -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw "Build falló" }
    }
}

if (-not (Test-Path -LiteralPath $exe)) {
    $hint = if ($preferPublish) {
        "Publique primero: .\scripts\Publish-Release.ps1`n  o: .\scripts\Update-DesktopShortcut.ps1 -Build"
    }
    else {
        "Compile primero: dotnet build $solution -c $Configuration`n  o: .\scripts\Update-DesktopShortcut.ps1 -UseBin -Build"
    }
    throw "No existe el ejecutable.`n  $exe`n$hint"
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
# WorkingDirectory = carpeta del exe (deps/config junto al publish).
$lnk.WorkingDirectory = [System.IO.Path]::GetDirectoryName($exe)
$lnk.Arguments = ""
$lnk.IconLocation = "$exe,0"
$lnk.Description = "Qubi Cost Electrical v$version"
$lnk.Save()

if ($RegisterAssociation) {
    # Espejo de ShellRegistration.Register() (HKCU, sin admin).
    $progId = "QubiCostElectrical.Project"
    $classes = "HKCU:\Software\Classes"
    New-Item -Path "$classes\.qce" -Force | Out-Null
    Set-ItemProperty -LiteralPath "$classes\.qce" -Name "(default)" -Value $progId
    New-Item -Path "$classes\$progId" -Force | Out-Null
    Set-ItemProperty -LiteralPath "$classes\$progId" -Name "(default)" -Value "Proyecto de Qubi Cost Electrical"
    New-Item -Path "$classes\$progId\DefaultIcon" -Force | Out-Null
    Set-ItemProperty -LiteralPath "$classes\$progId\DefaultIcon" -Name "(default)" -Value "`"$exe`",0"
    New-Item -Path "$classes\$progId\shell\open\command" -Force | Out-Null
    Set-ItemProperty -LiteralPath "$classes\$progId\shell\open\command" -Name "(default)" -Value "`"$exe`" `"%1`""
    Write-Host "Asociación .qce registrada (HKCU → $progId)"
}

$built = (Get-Item -LiteralPath $exe).LastWriteTime
Write-Host "Acceso directo actualizado: $ShortcutPath"
Write-Host "  Ejecutable: $exe"
Write-Host "  Versión:    v$version"
Write-Host "  Compilado:  $built"
