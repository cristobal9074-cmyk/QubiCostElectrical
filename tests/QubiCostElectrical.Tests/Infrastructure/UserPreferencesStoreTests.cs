using System;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

[Collection("WpfHost")]
public sealed class UserPreferencesStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"prefs_test_{Guid.NewGuid():N}", "preferences.json");

    [Fact]
    public void SaveAndLoad_RoundTripsGlobalVisualPreferences()
    {
        using var scope = UserPreferencesStore.UsePreferencesFilePathForTests(TempPath());
        string path = UserPreferencesStore.PreferencesFilePath;
        try
        {
            var prefs = new UserPreferences
            {
                FontSize = 10.5f,
                DarkMode = true,
                ThemeName = "Dark",
                SidebarCompactMode = false,
                MaterialsPageSize = 500,
                AutosaveEnabled = true,
                PriceFeedUrls = { "https://example.com/feed.json" },
                LetterheadCompanyName = "Empresa Test SpA"
            };

            UserPreferencesStore.Save(prefs);
            var loaded = UserPreferencesStore.Load();

            Assert.Equal(10.5f, loaded.FontSize);
            Assert.True(loaded.DarkMode);
            Assert.Equal("Dark", loaded.ThemeName);
            Assert.False(loaded.SidebarCompactMode);
            Assert.Equal(500, loaded.MaterialsPageSize);
            Assert.True(loaded.AutosaveEnabled);
            Assert.Contains("https://example.com/feed.json", loaded.PriceFeedUrls);
            Assert.Equal("Empresa Test SpA", loaded.LetterheadCompanyName);
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch { }
        }
    }
}
