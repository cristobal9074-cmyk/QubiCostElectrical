namespace QubiCostElectrical.Wpf.Infrastructure;

public interface IFileDialogService
{
    string? PromptSaveFile(string title, string defaultName, string filter);
    string? PromptOpenFile(string filter);
    string? PromptFolder(string description);
}
