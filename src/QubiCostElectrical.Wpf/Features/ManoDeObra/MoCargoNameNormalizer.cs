using System;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public static class MoCargoNameNormalizer
{
    public static bool IsGuidLike(string? value)
    {
        var v = value?.Trim();
        if (v is null)
            return false;

        if (v.Length == 36 && v[8] == '-' && v[13] == '-' && v[18] == '-' && v[23] == '-')
            v = v.Replace("-", "");
        if (v.Length != 32)
            return false;

        foreach (var ch in v)
        {
            if (!Uri.IsHexDigit(ch))
                return false;
        }

        return true;
    }

    public static string ResolveDirectaNombre(string? cargo, int index)
    {
        if (!IsGuidLike(cargo))
            return cargo ?? string.Empty;

        if (index <= 0)
            return "Maestro";
        if (index == 1)
            return "Ayudante";
        return "Ayudante " + index;
    }
}
