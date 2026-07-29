using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public static class MaterialImportService
{
    public static string NextNewMaterialCode(IReadOnlyList<MaterialRow> materiales)
    {
        ArgumentNullException.ThrowIfNull(materiales);
        int seq = materiales.Count + 1;
        string code;
        do
        {
            code = $"NUEVO-{seq:000}";
            seq++;
        }
        while (materiales.Any(m => m.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase)));
        return code;
    }

    public static (int Imported, int Updated) MergeSpreadsheetRows(
        IReadOnlyList<MaterialSpreadsheetRow> rows,
        List<MaterialRow> materiales,
        Func<decimal, decimal> convertToUf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(materiales);
        ArgumentNullException.ThrowIfNull(convertToUf);

        int imported = 0;
        int updated  = 0;

        foreach (var row in rows)
        {
            decimal matUf = convertToUf(row.MatValue);
            decimal moUf  = convertToUf(row.MoValue);

            var existing = materiales.FirstOrDefault(m =>
                m.Codigo.Equals(row.Codigo, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Descripcion = row.Descripcion;
                existing.Unidad      = row.Unidad;
                existing.MatUf       = matUf;
                existing.MoUf        = moUf;
                existing.UltimaActualizacionPrecio = AppClock.Now;
                updated++;
            }
            else
            {
                materiales.Add(new MaterialRow
                {
                    Codigo       = row.Codigo,
                    Descripcion  = row.Descripcion,
                    Unidad       = row.Unidad,
                    Categoria    = "General",
                    Subcategoria = "Sin clasificar",
                    MatUf        = matUf,
                    MoUf         = moUf,
                    FechaPrecio  = DateTime.Today
                });
                imported++;
            }
        }

        return (imported, updated);
    }

    public static int MergeClipboardText(
        string text,
        List<MaterialRow> materiales,
        Func<string> nextCode,
        Func<decimal, decimal> convertToUf)
    {
        ArgumentNullException.ThrowIfNull(materiales);
        ArgumentNullException.ThrowIfNull(nextCode);
        ArgumentNullException.ThrowIfNull(convertToUf);

        if (string.IsNullOrWhiteSpace(text)) return 0;

        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int processed = 0;
        foreach (string line in lines)
        {
            string[] cells = line.Split('\t');
            if (cells.Length < 2) continue;
            string code = cells[0].Trim();
            string desc = cells[1].Trim();
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(desc)) continue;

            var existing = materiales.FirstOrDefault(m =>
                m.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Descripcion = desc;
                if (cells.Length >= 3) existing.Unidad           = string.IsNullOrWhiteSpace(cells[2].Trim()) ? "UN"      : cells[2].Trim();
                if (cells.Length >= 4) existing.Categoria        = string.IsNullOrWhiteSpace(cells[3].Trim()) ? "General" : cells[3].Trim();
                if (cells.Length >= 5) existing.Subcategoria     = cells[4].Trim();
                if (cells.Length >= 6) existing.Subcategoria2    = cells[5].Trim();
                if (cells.Length >= 7) existing.TipoInstalacion  = cells[6].Trim();
                if (cells.Length >= 8) existing.EspecificacionTecnica = cells[7].Trim();
                if (cells.Length >= 9) existing.Marca            = cells[8].Trim();
                if (cells.Length >= 10) existing.EquivalenciaKey  = cells[9].Trim();
                if (cells.Length >= 11) existing.Proveedor        = cells[10].Trim();
                if (cells.Length >= 12 && DateTime.TryParse(cells[11].Trim(), out var fp))
                    existing.FechaPrecio = fp;
                if (cells.Length >= 13) existing.MatUf = convertToUf(ParseDecimal(cells[12].Trim()));
                if (cells.Length >= 14) existing.MoUf  = convertToUf(ParseDecimal(cells[13].Trim()));
                existing.UltimaActualizacionPrecio = AppClock.Now;
            }
            else
            {
                materiales.Add(new MaterialRow
                {
                    Codigo          = string.IsNullOrWhiteSpace(code) ? nextCode() : code,
                    Descripcion     = desc,
                    Unidad          = cells.Length >= 3 && !string.IsNullOrWhiteSpace(cells[2].Trim()) ? cells[2].Trim() : "UN",
                    Categoria       = cells.Length >= 4 && !string.IsNullOrWhiteSpace(cells[3].Trim()) ? cells[3].Trim() : "General",
                    Subcategoria    = cells.Length >= 5  ? cells[4].Trim()  : "",
                    Subcategoria2   = cells.Length >= 6  ? cells[5].Trim()  : "",
                    TipoInstalacion = cells.Length >= 7  ? cells[6].Trim()  : "",
                    EspecificacionTecnica = cells.Length >= 8 ? cells[7].Trim() : "",
                    Marca           = cells.Length >= 9  ? cells[8].Trim()  : "",
                    EquivalenciaKey = cells.Length >= 10 ? cells[9].Trim()  : "",
                    Proveedor       = cells.Length >= 11 ? cells[10].Trim() : "",
                    FechaPrecio     = cells.Length >= 12 && DateTime.TryParse(cells[11].Trim(), out var fecha)
                                          ? fecha : DateTime.Today,
                    MatUf = cells.Length >= 13 ? convertToUf(ParseDecimal(cells[12].Trim())) : 0m,
                    MoUf  = cells.Length >= 14 ? convertToUf(ParseDecimal(cells[13].Trim())) : 0m
                });
            }
            processed++;
        }

        return processed;
    }

    private static decimal ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            return result;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
            return result;
        return 0m;
    }
}
