using System.IO;
using Microsoft.Win32;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class WpfFileDialogService : IFileDialogService
{
    public string? PromptSaveFile(string title, string defaultName, string filter)
    {
        while (true)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultName,
                DefaultExt = Path.GetExtension(defaultName),
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog() != true)
                return null;

            if (OperationFailureMessages.IsFileLocked(dialog.FileName))
            {
                WpfDialogService.Error(
                    "Archivo en uso",
                    $"No se puede guardar en «{Path.GetFileName(dialog.FileName)}» porque está abierto en otra aplicación (por ejemplo Excel o Adobe Reader).\n\nCierra el archivo en la otra aplicación antes de continuar o elige otro nombre.");
                defaultName = dialog.FileName;
                continue;
            }

            return dialog.FileName;
        }
    }

    public string? PromptOpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PromptFolder(string description)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
