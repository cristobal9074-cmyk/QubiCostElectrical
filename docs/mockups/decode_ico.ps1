Add-Type -AssemblyName PresentationCore
$icoPath = "src/QubiCostElectrical.Wpf/Assets/QubiCostElectrical.ico"
$stream = [System.IO.File]::OpenRead($icoPath)
$decoder = [System.Windows.Media.Imaging.IconBitmapDecoder]::new($stream, [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::Default)

# Get the highest resolution frame (usually 256x256)
$frame = $decoder.Frames | Sort-Object { $_.PixelWidth } -Descending | Select-Object -First 1

$encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add($frame)
$outStream = [System.IO.File]::Create("docs/mockups/app_logo_from_ico.png")
$encoder.Save($outStream)
$outStream.Dispose()
$stream.Dispose()
Write-Output "Successfully decoded frame: $($frame.PixelWidth)x$($frame.PixelHeight)"
