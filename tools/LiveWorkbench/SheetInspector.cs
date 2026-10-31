using System;
using System.IO;
using ClosedXML.Excel;

namespace QubiCostElectrical.Tools.LiveWorkbench;

public static class SheetInspector
{
    public static void Inspect(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        Console.WriteLine($"Total hojas en '{Path.GetFileName(filePath)}': {workbook.Worksheets.Count}");
        foreach (var ws in workbook.Worksheets)
        {
            var range = ws.RangeUsed();
            int rows = range?.RowCount() ?? 0;
            int cols = range?.ColumnCount() ?? 0;
            Console.WriteLine($"Hoja: '{ws.Name,-25}' | Filas: {rows,6} | Cols: {cols,4}");
        }

        string[] targets = { "-=RESUMEN INGELSAM=-", "-=COSTOS=-", "MANO DE OBRA", "EPP" };
        foreach (var name in targets)
        {
            var ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (ws == null) continue;
            Console.WriteLine($"\n================== CONTENIDO HOJA: {ws.Name} (Muestra 30 filas) ==================");
            var r = ws.RangeUsed();
            if (r == null) continue;
            int rowCount = Math.Min(35, r.RowCount());
            int colCount = Math.Min(12, r.ColumnCount());
            for (int i = 1; i <= rowCount; i++)
            {
                var row = ws.Row(i);
                var vals = new List<string>();
                for (int j = 1; j <= colCount; j++)
                {
                    string txt = row.Cell(j).GetString().Trim();
                    if (!string.IsNullOrEmpty(txt)) vals.Add($"[C{j}]:{txt}");
                }
                if (vals.Count > 0)
                {
                    Console.WriteLine($"Fila {i,2}: " + string.Join(" | ", vals));
                }
            }
        }
    }
}
