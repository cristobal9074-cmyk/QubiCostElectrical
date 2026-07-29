namespace QubiCostElectrical;

public static class ProjectFileTypes
{
    public const string CurrentExtension = ".qce";
    public const string LegacyExtension = ".pepro";
    public const string AutoSaveSuffix = ".autosave";
    public const string SqliteSuffix = ".db";

    public static IReadOnlyList<string> SupportedExtensions { get; } =
        new[] { CurrentExtension, LegacyExtension };

    public static bool IsSupportedProjectExtension(string ext) =>
        ext.Equals(CurrentExtension, StringComparison.OrdinalIgnoreCase)
        || ext.Equals(LegacyExtension, StringComparison.OrdinalIgnoreCase);
}
