Add-Type -AssemblyName System.Drawing
$srcPath = "C:\Users\cristobal1\.gemini\antigravity\brain\cc2954a6-8df0-4567-acfd-065f8a69ca39\.user_uploaded\media_1793128632391_b7705101.png"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

$mockupDir = "c:\Users\cristobal1\Desktop\QubiCost Electrical\docs\mockups"
if (!(Test-Path $mockupDir)) {
    New-Item -ItemType Directory -Path $mockupDir -Force | Out-Null
}

# 1. Full reference image
$bmp.Save("$mockupDir\autocad_reference.png", [System.Drawing.Imaging.ImageFormat]::Png)

# 2. Extract right artwork (from x=420 to width - 20, y=90 to 575)
$rect = New-Object System.Drawing.Rectangle(430, 95, ($bmp.Width - 430 - 20), (575 - 95))
$sculpture = $bmp.Clone($rect, $bmp.PixelFormat)
$sculpture.Save("$mockupDir\autodesk_sculpture.png", [System.Drawing.Imaging.ImageFormat]::Png)

$bmp.Dispose()
$sculpture.Dispose()
Write-Output "Successfully extracted artwork!"
