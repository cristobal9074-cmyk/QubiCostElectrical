using System;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Una sola frase para la barra inferior. Si está vacía, el shell muestra el atajo del módulo.
/// </summary>
public static class ShellActivity
{
    public static event Action? Changed;

    public static string Text { get; private set; } = "";

    public static bool IsAlert { get; private set; }

    public static void Report(string? text)
    {
        string value = text?.Trim() ?? "";
        bool alert = value.Contains("no se pudo", StringComparison.OrdinalIgnoreCase)
            || value.Contains("no se aplic", StringComparison.OrdinalIgnoreCase)
            || value.Contains("sin coincidencias", StringComparison.OrdinalIgnoreCase)
            || value.Contains("error", StringComparison.OrdinalIgnoreCase)
            || value.Contains("vacío", StringComparison.OrdinalIgnoreCase)
            || value.Contains("vacio", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(Text, value, StringComparison.Ordinal) && IsAlert == alert)
            return;

        Text = value;
        IsAlert = alert && value.Length > 0;
        Changed?.Invoke();
    }
}
