using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Tools.LiveWorkbench;

public static class ItemizadoSpecificTester
{
    public static void Run(string excelPath, string targetQce)
    {
        using var wb = new XLWorkbook(excelPath);
        var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("ITEMIZADO", StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheets.First();

        Console.WriteLine($"\n=== PARSEANDO HOJA ESPECÍFICA: '{ws.Name}' ===");
        var range = ws.RangeUsed();
        if (range == null)
        {
            Console.WriteLine("Hoja vacía.");
            return;
        }

        var table = new List<List<string>>();
        foreach (var row in range.RowsUsed())
        {
            var rList = new List<string>();
            foreach (var cell in row.Cells(1, range.ColumnCount()))
            {
                rList.Add(cell.GetString());
            }
            table.Add(rList);
        }

        Console.WriteLine($"Filas extraídas: {table.Count}, Columnas: {range.ColumnCount()}");
        var (hRow, extraHRow) = ItemizadoImportParser.FindHeaderRowRange(table, "itemizado");
        Console.WriteLine($"Header detectado en fila: {hRow + 1} (extra: {extraHRow + 1})");
        if (hRow >= 0)
        {
            Console.WriteLine("Encabezados encontrados: " + string.Join(" | ", table[hRow]));
        }

        var useCase = new ImportarItemizadoUseCase();
        var result = useCase.Execute(table);
        if (!string.IsNullOrEmpty(result.Error))
        {
            Console.WriteLine($"[ERROR EN HOJA ITEMIZADO]: {result.Error}");
            return;
        }

        Console.WriteLine($"[ÉXITO] Partidas estructuradas: {result.Rows.Count}");
        var capitulos = result.Rows.Where(r => r.EsTitulo).ToList();
        var partidas = result.Rows.Where(r => !r.EsTitulo).ToList();
        Console.WriteLine($"Capítulos / Títulos: {capitulos.Count}");
        Console.WriteLine($"Partidas de obra   : {partidas.Count}");

        Console.WriteLine("\nPrimeros 15 registros de la hoja ITEMIZADO:");
        foreach (var r in result.Rows.Take(15))
        {
            string tipo = r.EsTitulo ? "[CAP]" : "[ITM]";
            decimal cant = r.CantidadOriginal > 0 ? r.CantidadOriginal : r.Cantidad;
            Console.WriteLine($"   {tipo} {r.Item,-8} | {r.Descripcion,-55} | Cant: {cant,6} | Un: {r.Unidad}");
        }

        // Guardar en QCE
        var projectData = new ProjectFileData();
        projectData.Proyecto.Nombre = Path.GetFileNameWithoutExtension(excelPath) + " (Itemizado Importado)";
        projectData.Proyecto.FechaCreacion = DateTime.Now;
        projectData.Proyecto.FechaModificacion = DateTime.Now;
        projectData.Proyecto.MonedaPresupuesto = "CLP";
        projectData.Proyecto.ValorUfClp = 37800m;
        projectData.Itemizado = result.Rows.ToList();

        SQLiteProjectStore.SaveSnapshot(targetQce, projectData);
        Console.WriteLine($"\n[OK] Guardado en: {targetQce} ({new FileInfo(targetQce).Length:N0} bytes)");
    }
}
