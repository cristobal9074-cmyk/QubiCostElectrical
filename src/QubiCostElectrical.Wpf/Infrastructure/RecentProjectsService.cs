using System.IO;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed record RecentProjectInfo(string Path, string Name, DateTime LastModified)
{
    public bool IsMissing => LastModified == DateTime.MinValue;
    public string DisplayDate => IsMissing
        ? "No encontrado"
        : LastModified.ToString("d MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("es-CL"));
}

public sealed class RecentProjectsService
{
    private const int MaxRecent = 8;

    public IReadOnlyList<RecentProjectInfo> GetProjects()
    {
        var prefs = UserPreferencesStore.Load();
        return prefs.RecentFiles
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(f =>
            {
                try
                {
                    return File.Exists(f)
                        ? new RecentProjectInfo(f, System.IO.Path.GetFileNameWithoutExtension(f), File.GetLastWriteTime(f))
                        : new RecentProjectInfo(f, System.IO.Path.GetFileNameWithoutExtension(f) ?? f, DateTime.MinValue);
                }
                catch
                {
                    return new RecentProjectInfo(f, System.IO.Path.GetFileNameWithoutExtension(f) ?? f, DateTime.MinValue);
                }
            })
            .ToList();
    }

    public void AddProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        path = Normalize(path);
        var prefs = UserPreferencesStore.Load();
        prefs.RecentFiles.RemoveAll(r => Equivalent(r, path));
        prefs.RecentFiles.Insert(0, path);
        while (prefs.RecentFiles.Count > MaxRecent)
            prefs.RecentFiles.RemoveAt(prefs.RecentFiles.Count - 1);
        UserPreferencesStore.Save(prefs);
    }

    public void RemoveProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        path = Normalize(path);
        var prefs = UserPreferencesStore.Load();
        int removed = prefs.RecentFiles.RemoveAll(r => Equivalent(r, path));
        if (removed > 0)
            UserPreferencesStore.Save(prefs);
    }

    public void RemoveMissing()
    {
        var prefs = UserPreferencesStore.Load();
        int removed = prefs.RecentFiles.RemoveAll(f => !File.Exists(f));
        if (removed > 0)
            UserPreferencesStore.Save(prefs);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string Normalize(string path)
    {
        try { return System.IO.Path.GetFullPath(path.Trim()); }
        catch { return path.Trim(); }
    }

    private static bool Equivalent(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
}
