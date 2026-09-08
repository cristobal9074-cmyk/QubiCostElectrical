using System.Diagnostics;
using QubiCostElectrical.Wpf.Infrastructure.Security;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class WpfFileOpenService : IFileOpenService
{
    public FileOpenResult OpenFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return FileOpenResult.FileNotFound;

        if (!System.IO.File.Exists(filePath))
            return FileOpenResult.FileNotFound;

        if (!SafeProcessLauncher.TryOpenFile(filePath, out var error))
        {
            Debug.WriteLine($"[WpfFileOpenService] No se pudo abrir {filePath}: {error}");
            return FileOpenResult.ViewerFailed;
        }

        return FileOpenResult.Success;
    }

    public void OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (!System.IO.Directory.Exists(folderPath))
            return;

        if (!SafeProcessLauncher.TryOpenFolder(folderPath, out var error))
        {
            Debug.WriteLine($"[WpfFileOpenService] No se pudo abrir carpeta {folderPath}: {error}");
        }
    }
}
