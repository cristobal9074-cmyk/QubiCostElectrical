param(
    [string]$ShortcutPath = 'C:\Users\cristobal1\Desktop\Qubi Cost Electrical.lnk',
    [string]$TargetPath = 'C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1\src\QubiCostElectrical.Wpf\bin\Release\net10.0-windows\QubiCostElectrical.exe',
    [string]$WorkingDirectory = 'C:\Users\cristobal1\Desktop\pruebas nuevo excel pega\1\src\QubiCostElectrical.Wpf\bin\Release\net10.0-windows',
    [string]$Arguments = ''
)

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($ShortcutPath)
$shortcut.TargetPath = $TargetPath
$shortcut.WorkingDirectory = $WorkingDirectory
$shortcut.Arguments = $Arguments
$shortcut.Save()

[PSCustomObject]@{
    ShortcutPath = $ShortcutPath
    TargetPath = $shortcut.TargetPath
    WorkingDirectory = $shortcut.WorkingDirectory
    Arguments = $shortcut.Arguments
}
