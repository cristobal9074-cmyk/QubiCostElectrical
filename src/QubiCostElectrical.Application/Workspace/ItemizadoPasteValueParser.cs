using System;

namespace QubiCostElectrical;

public static class ItemizadoPasteValueParser
{
    public static bool TryParseExcluidoCubicacion(string? value, out bool excluded)
    {
        excluded = false;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string v = value.Trim().ToUpperInvariant();
        if (v is "S" or "SI" or "SÍ" or "1" or "X" or "TRUE" or "VERDADERO" or "YES")
        {
            excluded = true;
            return true;
        }

        if (v is "N" or "NO" or "0" or "FALSE" or "FALSO")
        {
            excluded = false;
            return true;
        }

        return false;
    }
}
