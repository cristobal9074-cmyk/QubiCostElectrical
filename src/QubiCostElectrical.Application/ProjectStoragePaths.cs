using System.IO;

namespace QubiCostElectrical;

public static class ProjectStoragePaths
{
    public static bool IsSqliteProjectFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (path.EndsWith(".pepro.db", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.EndsWith(".qce.db", StringComparison.OrdinalIgnoreCase))
            return true;

        string ext = Path.GetExtension(path);
        return ProjectFileTypes.IsSupportedProjectExtension(ext)
            || ext.Equals(".db", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsJsonProjectFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    public static string AutoSaveDirectory() => ProductPaths.AutoSaveDirectory();

    public static string SQLiteDirectory() => ProductPaths.DatabaseDirectory();

    public static string GetAutoSavePathForProject(string projectPath, string fallbackProjectName)
    {
        string dir = Path.GetDirectoryName(projectPath) ?? AutoSaveDirectory();
        string name = Path.GetFileNameWithoutExtension(projectPath);
        if (string.IsNullOrWhiteSpace(name))
            name = fallbackProjectName;

        string ext = string.IsNullOrWhiteSpace(projectPath)
            ? ProjectFileTypes.CurrentExtension
            : Path.GetExtension(projectPath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ProjectFileTypes.CurrentExtension;
        return Path.Combine(dir, name + ext + ProjectFileTypes.AutoSaveSuffix);
    }

    public static string GetSqliteSidecarPath(IProjectRepository repository, string projectPath, string fallbackProjectName)
    {
        return repository.GetSQLiteDatabasePathForProject(
            projectPath,
            SQLiteDirectory(),
            fallbackProjectName);
    }
}
