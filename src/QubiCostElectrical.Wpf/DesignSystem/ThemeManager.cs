using System;
using System.Windows;

namespace QubiCostElectrical.Wpf.DesignSystem;

public static class ThemeManager
{
    private static bool _isDark;
    private static ResourceDictionary? _darkDict;

    public static bool IsDarkMode => _isDark;

    public static event EventHandler<bool>? ThemeChanged;

    public static void Initialize()
    {
        _darkDict = new ResourceDictionary
        {
            Source = new Uri("DesignSystem/Colors.Dark.xaml", UriKind.Relative)
        };

        _isDark = false;
    }

    public static void SetDarkMode(bool isDark)
    {
        if (isDark == _isDark) return;
        _isDark = isDark;

        if (isDark)
        {
            if (_darkDict != null && !Application.Current.Resources.MergedDictionaries.Contains(_darkDict))
            {
                // Insert at the beginning or replace to ensure it overrides colors key-by-key
                Application.Current.Resources.MergedDictionaries.Insert(0, _darkDict);
            }
        }
        else
        {
            if (_darkDict != null)
                Application.Current.Resources.MergedDictionaries.Remove(_darkDict);
        }

        ThemeChanged?.Invoke(null, isDark);
    }

    public static void Toggle()
    {
        SetDarkMode(!_isDark);
    }
}
