namespace QubiCostElectrical.Wpf.Infrastructure;

public enum FileOpenResult
{
    Success,
    FileNotFound,
    ViewerFailed
}

public interface IFileOpenService
{
    FileOpenResult OpenFile(string filePath);
    void OpenFolder(string folderPath);
}
