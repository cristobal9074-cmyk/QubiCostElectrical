using System.Diagnostics;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class WpfFileOpenService : IFileOpenService
{
    public FileOpenResult OpenFile(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
            return FileOpenResult.FileNotFound;

        try
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            return FileOpenResult.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WpfFileOpenService] No se pudo abrir {filePath}: {ex.Message}");
            return FileOpenResult.ViewerFailed;
        }
    }

    public void OpenFolder(string folderPath)
    {
        if (!System.IO.Directory.Exists(folderPath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WpfFileOpenService] No se pudo abrir carpeta {folderPath}: {ex.Message}");
        }
    }
}
