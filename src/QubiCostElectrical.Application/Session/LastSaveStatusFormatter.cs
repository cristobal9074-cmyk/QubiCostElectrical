using System;

namespace QubiCostElectrical;

public static class LastSaveStatusFormatter
{
    public static string Format(DateTime savedAt)
    {
        if (savedAt == DateTime.MinValue)
            return "sin registro";

        var elapsed = AppClock.Now - savedAt;
        if (elapsed.TotalSeconds < 15)
            return "ahora";
        if (elapsed.TotalMinutes < 60)
            return $"hace {(int)Math.Max(1, elapsed.TotalMinutes)} min";

        if (savedAt.Date == DateTime.Today)
            return savedAt.ToString("HH:mm");

        if (savedAt.Date == DateTime.Today.AddDays(-1))
            return $"ayer {savedAt:HH:mm}";

        return savedAt.ToString("dd/MM HH:mm");
    }

    public static string FormatSavedOkLabel(DateTime savedAt)
        => savedAt == DateTime.MinValue ? "Sin guardar aún" : $"Guardado OK · {Format(savedAt)}";
}
