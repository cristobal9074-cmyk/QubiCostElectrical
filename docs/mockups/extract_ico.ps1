Add-Type -AssemblyName System.Drawing
$icoPath = "src/QubiCostElectrical.Wpf/Assets/QubiCostElectrical.ico"
$outPng = "docs/mockups/app_logo_from_ico.png"

$ico = New-Object System.Drawing.Icon($icoPath, 256, 256)
$bmp = $ico.ToBitmap()
$bmp.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
$ico.Dispose()
$bmp.Dispose()
Write-Output "Successfully converted ICO to 256x256 PNG!"
