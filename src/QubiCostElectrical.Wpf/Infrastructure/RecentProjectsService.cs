using System.IO;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed record RecentProjectInfo(string Path, string Name, DateTime LastModified, bool IsPinned = false)
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
        var pinnedSet = new HashSet<string>(prefs.PinnedRecentFiles.Select(Normalize), StringComparer.OrdinalIgnoreCase);

        var projects = prefs.RecentFiles
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(f =>
            {
                var isPinned = pinnedSet.Contains(Normalize(f));
                try
                {
                    return File.Exists(f)
                        ? new RecentProjectInfo(f, System.IO.Path.GetFileNameWithoutExtension(f), File.GetLastWriteTime(f), isPinned)
                        : new RecentProjectInfo(f, System.IO.Path.GetFileNameWithoutExtension(f) ?? f, DateTime.MinValue, isPinned);
                }
                catch
                {
                    return new RecentProjectInfo(f, System.IO.Path.GetFileNameWithoutExtension(f) ?? f, DateTime.MinValue, isPinned);
                }
            })
            .ToList();

        // Los proyectos fijados van primero, conservando el orden de inserción dentro de cada grupo
        return projects
            .OrderByDescending(p => p.IsPinned)
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
        {
            var toRemove = prefs.RecentFiles[^1];
            prefs.RecentFiles.RemoveAt(prefs.RecentFiles.Count - 1);
            prefs.PinnedRecentFiles.RemoveAll(r => Equivalent(r, toRemove));
        }
        UserPreferencesStore.Save(prefs);
    }

    public bool TogglePin(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        path = Normalize(path);
        var prefs = UserPreferencesStore.Load();
        bool nowPinned;

        if (prefs.PinnedRecentFiles.Any(r => Equivalent(r, path)))
        {
            prefs.PinnedRecentFiles.RemoveAll(r => Equivalent(r, path));
            nowPinned = false;
        }
        else
        {
            if (!prefs.RecentFiles.Any(r => Equivalent(r, path)))
                prefs.RecentFiles.Insert(0, path);

            prefs.PinnedRecentFiles.Add(path);
            nowPinned = true;
        }

        UserPreferencesStore.Save(prefs);
        return nowPinned;
    }

    public bool IsPinned(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var prefs = UserPreferencesStore.Load();
        return prefs.PinnedRecentFiles.Any(r => Equivalent(r, path));
    }

    public void RemoveProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        path = Normalize(path);
        var prefs = UserPreferencesStore.Load();
        int removedRecent = prefs.RecentFiles.RemoveAll(r => Equivalent(r, path));
        int removedPinned = prefs.PinnedRecentFiles.RemoveAll(r => Equivalent(r, path));
        if (removedRecent > 0 || removedPinned > 0)
            UserPreferencesStore.Save(prefs);
    }

    public void RemoveMissing()
    {
        var prefs = UserPreferencesStore.Load();
        int removedRecent = prefs.RecentFiles.RemoveAll(f => !File.Exists(f));
        int removedPinned = prefs.PinnedRecentFiles.RemoveAll(f => !File.Exists(f));
        if (removedRecent > 0 || removedPinned > 0)
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
