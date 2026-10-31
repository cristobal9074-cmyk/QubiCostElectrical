using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Tools.LiveWorkbench;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("==========================================================");
        Console.WriteLine("   QUBI COST ELECTRICAL — WORKBENCH DE OPERACIONES EN VIVO");
        Console.WriteLine("==========================================================");

        string pricesFile = @"C:\Users\cristobal1\Downloads\Plantilla_Lista_Precios_Qubi.xlsx";
        string excelProjectFile = @"C:\Users\cristobal1\Downloads\PEL039A2026 ABIERTO.xlsx";
        string outputProjectFile = @"C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL039A2026_IMPORTADO_AUTO.qce";

        bool runCsv = args.Contains("--generate-csv");
        if (runCsv)
        {
            string csvTarget = @"src\QubiCostElectrical.Infrastructure\PriceBank\banco_inicial.csv";
            EmbeddedCsvGenerator.Generate(pricesFile, csvTarget);
            return 0;
        }

        if (args.Contains("--inspect"))
        {
            SheetInspector.Inspect(excelProjectFile);
            return 0;
        }

        bool run1 = args.Length == 0 || args.Contains("1") || args.Contains("--prices");
        bool run2 = args.Length == 0 || args.Contains("2") || args.Contains("--itemizado");

        if (run1)
        {
            EjecutarCargaBancoPrecios(pricesFile);
        }

        if (run2)
        {
            ItemizadoSpecificTester.Run(excelProjectFile, outputProjectFile);
        }

        Console.WriteLine("\n[FINALIZADO CON ÉXITO]");
        return 0;
    }

    private static void EjecutarCargaBancoPrecios(string path)
    {
        Console.WriteLine("\n----------------------------------------------------------");
        Console.WriteLine(" TAREA 1: CARGAR LISTA DE PRECIOS EN BANCO CENTRAL");
        Console.WriteLine("----------------------------------------------------------");

        if (!File.Exists(path))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Archivo no encontrado: {path}");
            Console.ResetColor();
            return;
        }

        string dbPath = ProductPaths.PriceBankDatabaseFile();
        Console.WriteLine($"Archivo Excel origen : {path}");
        Console.WriteLine($"Base de datos destino: {dbPath}");

        var store = new PriceBankStore(dbPath);
        int initialCount = store.Count();
        Console.WriteLine($"Items actuales en banco: {initialCount}");

        Console.WriteLine("Parseando archivo Excel con PriceBankSeeder...");
        var parsed = PriceBankSeeder.ParseFile(path);
        Console.WriteLine($"Registros válidos extraídos del Excel: {parsed.Count}");

        if (parsed.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] No se encontraron registros válidos para importar.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine("Insertando / actualizando registros (UpsertMany)...");
        int processed = store.UpsertMany(parsed);
        int finalCount = store.Count();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] {processed} registros procesados.");
        Console.WriteLine($"[OK] Total de materiales ahora en banco: {finalCount}");
        Console.ResetColor();

        // Familias cargadas
        var allEntries = store.GetAll();
        var fams = allEntries.GroupBy(x => x.Categoria).OrderByDescending(g => g.Count());
        Console.WriteLine("Distribución por familias en base de datos:");
        foreach (var f in fams)
        {
            string name = string.IsNullOrWhiteSpace(f.Key) ? "(Sin familia)" : f.Key;
            Console.WriteLine($"   - {name,-20}: {f.Count()} insumos");
        }
    }

    private static void EjecutarImportacionItemizado(string sourceExcel, string targetQce)
    {
        Console.WriteLine("\n----------------------------------------------------------");
        Console.WriteLine(" TAREA 2: IMPORTAR ITEMIZADO REAL DESDE EXCEL MANDANTE");
        Console.WriteLine("----------------------------------------------------------");

        if (!File.Exists(sourceExcel))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Archivo no encontrado: {sourceExcel}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Archivo Excel mandante: {sourceExcel}");
        Console.WriteLine($"Destino proyecto QCE  : {targetQce}");

        var reader = new ItemizadoSpreadsheetReader();
        Console.WriteLine("Leyendo hojas del libro con ItemizadoSpreadsheetReader...");
        var tableReadOnly = reader.ReadFirstWorksheet(sourceExcel);

        var table = tableReadOnly.Select(r => r.ToList()).ToList();
        Console.WriteLine($"Filas brutas leídas de la mejor hoja: {table.Count}");

        var (hRow, extraHRow) = ItemizadoImportParser.FindHeaderRowRange(table, "itemizado");
        Console.WriteLine($"Fila de encabezado detectada: {hRow + 1} (Índice 0-based: {hRow})");

        var useCase = new ImportarItemizadoUseCase();
        var result = useCase.Execute(table);

        if (!string.IsNullOrEmpty(result.Error))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR IMPORTACIÓN] {result.Error}");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] Partidas estructuradas con éxito: {result.Rows.Count}");
        Console.ResetColor();

        // Muestra de partidas
        Console.WriteLine("\nMuestra de partidas importadas:");
        var capitulos = result.Rows.Where(r => r.EsTitulo).ToList();
        var partidasMedidas = result.Rows.Where(r => !r.EsTitulo).ToList();

        Console.WriteLine($"Capítulos / Títulos       : {capitulos.Count}");
        Console.WriteLine($"Partidas con medición    : {partidasMedidas.Count}");
        Console.WriteLine($"Partidas con Cantidad > 0: {partidasMedidas.Count(p => p.CantidadOriginal > 0 || p.Cantidad > 0)}");

        Console.WriteLine("\nPrimeros 8 ítems en jerarquía:");
        foreach (var r in result.Rows.Take(8))
        {
            string tipo = r.EsTitulo ? "[CAP]" : "[ITM]";
            decimal cant = r.CantidadOriginal > 0 ? r.CantidadOriginal : r.Cantidad;
            Console.WriteLine($"   {tipo} {r.Item,-10} | {r.Descripcion,-45} | Cant: {cant,8} | Un: {r.Unidad}");
        }

        // Guardar en .qce (SQLite)
        Console.WriteLine($"\nGuardando en formato nativo SQLite (.qce): {targetQce}...");
        string targetDir = Path.GetDirectoryName(targetQce) ?? ".";
        Directory.CreateDirectory(targetDir);

        var projectData = new ProjectFileData();
        projectData.Proyecto.Nombre = "PEL039A2026 - Edificio Sara del Campo (Importado)";
        projectData.Proyecto.FechaCreacion = DateTime.Now;
        projectData.Proyecto.FechaModificacion = DateTime.Now;
        projectData.Proyecto.MonedaPresupuesto = "CLP";
        projectData.Proyecto.ValorUfClp = 37800m;
        projectData.Itemizado = result.Rows.ToList();

        SQLiteProjectStore.SaveSnapshot(targetQce, projectData);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] Proyecto .qce generado con {new FileInfo(targetQce).Length:N0} bytes.");
        Console.WriteLine($"[OK] Listo para abrir en Qubi Cost Electrical (Dev).");
        Console.ResetColor();
    }
}
