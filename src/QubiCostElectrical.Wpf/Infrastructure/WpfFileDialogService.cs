using System.IO;
using Microsoft.Win32;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class WpfFileDialogService : IFileDialogService
{
    public string? PromptSaveFile(string title, string defaultName, string filter)
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
        return dialog.ShowDialog() == true ? dialog.FileName : null;
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
