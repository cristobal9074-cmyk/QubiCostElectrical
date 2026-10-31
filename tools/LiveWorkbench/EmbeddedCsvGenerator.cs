using System;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Tools.LiveWorkbench;

public static class EmbeddedCsvGenerator
{
    public static void Generate(string xlsxPath, string targetCsv)
    {
        Console.WriteLine($"Leyendo con ClosedXML: {xlsxPath}");
        var entries = PriceBankSeeder.ParseFile(xlsxPath);
        Console.WriteLine($"Entradas parseadas: {entries.Count}");

        var sb = new StringBuilder();
        // Header
        sb.AppendLine("Codigo;Descripcion;Unidad;Familia;PrecioCLP;MO;Rendimiento;OrigenPrecio");

        var unique = entries.GroupBy(e => e.Codigo).Select(g => g.Last()).ToList();
        Console.WriteLine($"Entradas únicas: {unique.Count}");

        foreach (var e in unique)
        {
            string cod = e.Codigo.Trim().ToUpperInvariant();
            string desc = (e.Descripcion ?? "").Replace(";", ",").Trim();
            string un = (e.Unidad ?? "").Replace(";", ",").Trim();
            string fam = (e.Categoria ?? "").Replace(";", ",").Trim();
            decimal p = e.MatClp;
            decimal mo = e.MoClp;
            decimal rend = e.RendimientoDh;

            sb.AppendLine($"{cod};{desc};{un};{fam};{p};{mo};{rend};Catálogo Base Chile 2026");
        }

        string fullDest = Path.GetFullPath(targetCsv);
        File.WriteAllText(fullDest, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Console.WriteLine($"[OK] Generado CSV canónico con {unique.Count} insumos en: {fullDest}");
    }
}
