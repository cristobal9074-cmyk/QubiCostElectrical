using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;

namespace QubiCostElectrical;

/// <summary>
/// Importa materiales desde Excel (.xlsx) o CSV al banco de precios.
/// Detecta columnas por nombre de cabecera (orden flexible).
/// Plantilla oficial: Código | Descripción | UN | Precio CLP | Familia | MO
/// </summary>
public static class PriceBankSeeder
{
    private static readonly string[] SeedFileNames = ["banco_inicial.xlsx", "banco_inicial.csv"];

    private const string EmbeddedResourceName = "QubiCostElectrical.PriceBank.banco_inicial.csv";

    /// <summary>Cabeceras canónicas de la plantilla de importación (misma UI del banco).</summary>
    public static readonly string[] ImportTemplateHeaders =
    [
        "Código",
        "Descripción",
        "UN",
        "Precio CLP",
        "Familia",
        "MO",
        "M.O. por %",
        "Porcentaje M.O.",
        "Rendimiento"
    ];

    public static string ImportFormatHelpMessage =>
        "Formato Qubi (fila 1, en este orden):\n" +
        "Código | Descripción | UN | Precio CLP | Familia | MO | M.O. por % | Porcentaje M.O. | Rendimiento\n\n" +
        "Descargue la plantilla desde Banco de precios, copie sus precios ahí e importe de nuevo.\n" +
        "Los precios van en pesos chilenos (CLP). Rendimiento (u/D-H) es opcional;\n" +
        "déjelo vacío si no usa el modo M.O. por rendimiento.\n" +
        "Para materiales sin M.O. propia use M.O. por % (Sí) + Porcentaje M.O. (2,5 = 2,5%):\n" +
        "la M.O. resultante es Precio × % × Índice FA del proyecto (0 = sin calibrar: se usa factor 1 " +
        "y el índice se deriva en Costo → Familias).";

    /// <summary>
    /// Escribe plantilla Excel (.xlsx) o CSV según extensión, con cabeceras oficiales + 2 filas de ejemplo.
    /// </summary>
    public static void WriteImportTemplate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".xlsx" or ".xls")
            WriteImportTemplateExcel(path);
        else
            WriteImportTemplateCsv(path);
    }

    private static void WriteImportTemplateExcel(string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Lista de precios");
        for (int c = 0; c < ImportTemplateHeaders.Length; c++)
            ws.Cell(1, c + 1).Value = ImportTemplateHeaders[c];

        ws.Cell(2, 1).Value = "H07Z1-K";
        ws.Cell(2, 2).Value = "Cable Cu libre de halógenos 1×2,5 mm²";
        ws.Cell(2, 3).Value = "ml";
        ws.Cell(2, 4).Value = 1250;
        ws.Cell(2, 5).Value = "Conductores";
        ws.Cell(2, 6).Value = 350;
        ws.Cell(2, 7).Value = "";
        ws.Cell(2, 8).Value = "";
        ws.Cell(2, 9).Value = 100;

        ws.Cell(3, 1).Value = "TPRPE20";
        ws.Cell(3, 2).Value = "Tubería PVC rígido 20 mm";
        ws.Cell(3, 3).Value = "ml";
        ws.Cell(3, 4).Value = 890;
        ws.Cell(3, 5).Value = "Canalización";
        ws.Cell(3, 6).Value = 120;
        ws.Cell(3, 7).Value = "";
        ws.Cell(3, 8).Value = "";
        ws.Cell(3, 9).Value = 40;

        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    private static void WriteImportTemplateCsv(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', ImportTemplateHeaders));
        sb.AppendLine("H07Z1-K;Cable Cu libre de halógenos 1×2,5 mm²;ml;1250;Conductores;350;;;100");
        sb.AppendLine("TPRPE20;Tubería PVC rígido 20 mm;ml;890;Canalización;120;;;40");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// Retorna los materiales del banco de precios embebido en el ensamblado.
    /// Siempre disponible sin depender de archivos externos.
    /// </summary>
    public static IReadOnlyList<PriceBankEntry> GetEmbeddedEntries()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
        if (stream == null) return [];
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return [];
        char separator = DetectSeparator(lines[0]);
        var headers = lines[0].Split(separator).Select(NormalizeHeader).ToArray();
        var result = new List<PriceBankEntry>(lines.Length - 1);
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i].Trim(), separator);
            var entry = MapRow(headers, cols);
            if (entry != null) result.Add(entry);
        }
        return result;
    }

    /// <summary>Busca un archivo de seed junto al ejecutable. Devuelve null si no existe.</summary>
    public static string? FindSeedFile(string baseDirectory)
    {
        foreach (string name in SeedFileNames)
        {
            string path = Path.Combine(baseDirectory, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>Parsea CSV o Excel según extensión.</summary>
    public static IReadOnlyList<PriceBankEntry> ParseFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".xlsx" or ".xls"
            ? ParseExcel(path)
            : ParseCsv(path);
    }

    /// <summary>
    /// Importa desde CSV con separador automático (coma o punto y coma).
    /// Cabeceras reconocidas (sin importar mayúsculas ni tildes):
    /// Codigo, Descripcion, Unidad, Categoria, MatUF, MoUF, Proveedor, Marca, FechaPrecio.
    /// </summary>
    public static IReadOnlyList<PriceBankEntry> ParseCsv(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length < 2) return [];

        char separator = DetectSeparator(lines[0]);
        var headers = lines[0].Split(separator).Select(NormalizeHeader).ToArray();
        var result = new List<PriceBankEntry>(lines.Length - 1);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var cols = SplitCsvLine(line, separator);
            var entry = MapRow(headers, cols);
            if (entry != null) result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Importa desde la primera hoja del Excel.
    /// La primera fila debe contener cabeceras.
    /// </summary>
    public static IReadOnlyList<PriceBankEntry> ParseExcel(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var rows = ws.RangeUsed()?.RowsUsed().ToList();
        if (rows == null || rows.Count < 2) return [];

        var headers = rows[0].Cells().Select(c => NormalizeHeader(c.GetString())).ToArray();
        var result = new List<PriceBankEntry>(rows.Count - 1);

        for (int i = 1; i < rows.Count; i++)
        {
            var cols = rows[i].Cells(1, headers.Length).Select(c => c.GetString()).ToArray();
            var entry = MapRow(headers, cols);
            if (entry != null) result.Add(entry);
        }

        return result;
    }

    private static PriceBankEntry? MapRow(string[] headers, string[] cols)
    {
        string? Get(params string[] candidates)
        {
            foreach (string c in candidates)
            {
                int idx = Array.IndexOf(headers, c);
                if (idx >= 0 && idx < cols.Length) return cols[idx].Trim();
            }
            return null;
        }

        string? codigo = Get("codigo", "code", "cod");
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        // El precio se ingresa SIEMPRE en pesos chilenos: PRECIO CLP (material) y MO (mano de
        // obra). Se acepta también el header legado "matuf"/"mouf" como pesos (no como UF) para
        // no romper archivos ya preparados con esos nombres de columna — el valor numérico se
        // interpreta igual, solo cambia el significado (antes UF, ahora CLP). MatUf/MoUf quedan
        // en 0 aquí; se derivan más adelante con el valor de UF vigente (ver
        // MaterialRow/PriceBankEntry.RecalcularUf, PriceBankEntryVm).
        var entry = new PriceBankEntry
        {
            Codigo = codigo.ToUpperInvariant(),
            Descripcion = Get("descripcion", "description", "nombre", "material", "item") ?? "",
            Unidad = Get("unidad", "unit", "um", "ud", "un") ?? "",
            Categoria = Get("familia", "categoria", "category", "rubro", "tipo") ?? "",
            MatClp = ParseDecimal(Get(
                "precioclp", "preciomaterial", "preciomat", "precio",
                "mat", "matclp", "matuf", "costomat")),
            MoClp = ParseDecimal(Get(
                "mo", "moclp", "manoobra", "manodeobra",
                "mouf", "costomo")),
            RendimientoDh = ParseDecimal(Get(
                "rendimiento", "rend", "renddh", "rendimientoudh",
                "udiapordh", "udiadh")),
            DiaXPareja = ParseDecimal(Get(
                "diapareja", "diaxpareja", "diasxpareja", "pareja")),
            MonedaPrecio = "CLP",
            Proveedor = Get("proveedor", "supplier", "vendor") ?? "",
            Marca = Get("marca", "brand") ?? "",
            FechaPrecio = ParseDate(Get("fecha", "fechaprecio")) ?? DateTime.Today,
            OrigenPrecio = "Banco inicial",
            ProyectoOrigen = "",
            ActualizadoEn = DateTime.Now
        };

        // Fórmula M.O. por % (paridad "=+E*0.025" de LISTA-PR): "M.O. por %" (Sí/No) y/o
        // "Porcentaje M.O." (2,5 = 2,5%). Un porcentaje > 0 en la columna activa la fórmula.
        // Los candidatos van en forma NORMALIZADA (sin puntos ni espacios): "PORCENTAJE M.O."
        // → "porcentajemo" y "M.O. por %" → "mopor" (ver NormalizeHeader).
        string? pctRaw = Get("porcentajemo", "porcentajematerial", "porcentmo", "pctmo", "mopct");
        if (!string.IsNullOrWhiteSpace(pctRaw))
        {
            decimal pct = ParseDecimal(pctRaw);
            if (pct > 0m)
            {
                entry.MoPorcentajeMaterial = pct;
                entry.MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje;
            }
        }

        string? formulaRaw = Get("mopor", "moporcentaje", "moporcent", "formulamo", "modomo");
        if (!string.IsNullOrWhiteSpace(formulaRaw))
        {
            bool activo = IsTruthy(formulaRaw);
            entry.MoFormulaTipo = activo ? MaterialMoResolver.MoFormulaPorcentaje : "";
        }

        return entry;
    }

    private static bool IsTruthy(string value)
    {
        string n = NormalizeHeader(value);
        return n is "si" or "s" or "1" or "true" or "x" or "porcentaje" or "activo";
    }

    private static char DetectSeparator(string header)
    {
        int semicolons = header.Count(c => c == ';');
        int commas = header.Count(c => c == ',');
        return semicolons >= commas ? ';' : ',';
    }

    private static string[] SplitCsvLine(string line, char sep)
    {
        // Respeta campos entre comillas dobles.
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == sep && !inQuotes) { result.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        result.Add(current.ToString());
        return [.. result];
    }

    private static string NormalizeHeader(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw.Trim().ToLowerInvariant())
        {
            char mapped = c switch
            {
                'á' or 'à' => 'a',
                'é' or 'è' => 'e',
                'í' or 'ì' => 'i',
                'ó' or 'ò' => 'o',
                'ú' or 'ù' => 'u',
                'ñ' => 'n',
                _ => c
            };
            if (char.IsLetterOrDigit(mapped))
                sb.Append(mapped);
        }
        return sb.ToString();
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        string clean = value.Replace("$", "").Replace(" ", "").Trim();
        // Detecta formato europeo (coma decimal) si tiene coma y no punto
        if (clean.Contains(',') && !clean.Contains('.'))
            clean = clean.Replace(',', '.');
        else
            clean = clean.Replace(",", "");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        return null;
    }
}
