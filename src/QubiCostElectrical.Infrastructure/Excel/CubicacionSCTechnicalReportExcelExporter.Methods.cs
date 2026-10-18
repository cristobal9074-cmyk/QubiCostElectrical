using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical.Infrastructure;

public sealed partial class CubicacionSCTechnicalReportExcelExporter : ICubicacionSCReportExcelExporter
{
    public CubicacionSCTechnicalReportExcelExporter()
        : this(new CurrencyConversionService())
    {
    }

    public CubicacionSCTechnicalReportExcelExporter(ICurrencyConversionService currencyConversion)
    {
        this.currencyConversion = currencyConversion ?? throw new ArgumentNullException(nameof(currencyConversion));
    }

    public void Export(ExportProjectContext context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de exportacion requerida.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var wb = new XLWorkbook();
        var tableros = context.Data.CubicacionSC ?? new List<CuadroCargaSCRow>();
        var config = context.Data.CubicacionScConfig ?? CubicacionScConfig.Default();
        var materials = CubicacionSCEngine.Aggregate(tableros, config);
        var catalog = context.Materiales
            .Where(m => !string.IsNullOrWhiteSpace(m.Descripcion))
            .GroupBy(m => m.Descripcion.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        BuildSummarySheet(wb, context, tableros, materials);
        BuildBoardSheet(wb, tableros);
        BuildCircuitSheet(wb, tableros);
        BuildMaterialsSheet(wb, context, materials, catalog);
        BuildConfigSheet(wb, config);
        BuildCuadroCargasSecSheet(wb, context, tableros);

        wb.SaveAs(path);
    }

    private static void BuildSummarySheet(XLWorkbook wb, ExportProjectContext ctx, IReadOnlyList<CuadroCargaSCRow> tableros, IReadOnlyDictionary<string, CubicacionSCEngine.MaterialSC> materials)
    {
        var ws = wb.Worksheets.Add("00_RESUMEN");
        ApplyBaseStyle(ws);

        WriteTitle(ws, "REPORTE TECNICO C.SEC");
        WriteProjectHeader(ws, ctx, 2);

        int totalCircuits = tableros.Sum(t => t.Circuitos.Count);
        int activeCircuits = tableros.Sum(t => t.Circuitos.Count(c => c.EsActivo));
        int reserveCircuits = tableros.Sum(t => t.Circuitos.Count(c => c.EsReservaEquipada));
        int totalCajas = tableros.Sum(t => t.Circuitos.Sum(c => c.TotalCajas));
        decimal totalDistance = tableros.Sum(t => t.Circuitos.Sum(c => c.DistanciaTablero));
        decimal totalCircuitLength = tableros.Sum(t => t.Circuitos.Sum(c => c.LargoTotal));

        int kpiRow = 5;
        WriteKpi(ws, kpiRow, 1, "Tableros", tableros.Count.ToString("N0", CultureInfo.InvariantCulture));
        WriteKpi(ws, kpiRow, 3, "Circuitos", totalCircuits.ToString("N0", CultureInfo.InvariantCulture));
        WriteKpi(ws, kpiRow, 5, "Activos", activeCircuits.ToString("N0", CultureInfo.InvariantCulture));
        WriteKpi(ws, kpiRow, 7, "Reservas", reserveCircuits.ToString("N0", CultureInfo.InvariantCulture));

        kpiRow += 4;
        WriteKpi(ws, kpiRow, 1, "Distancia", totalDistance.ToString("N2", CultureInfo.InvariantCulture) + " m");
        WriteKpi(ws, kpiRow, 3, "Largo", totalCircuitLength.ToString("N2", CultureInfo.InvariantCulture) + " m");
        WriteKpi(ws, kpiRow, 5, "Cajas", totalCajas.ToString("N0", CultureInfo.InvariantCulture));
        WriteKpi(ws, kpiRow, 7, "Materiales", materials.Count.ToString("N0", CultureInfo.InvariantCulture));

        int row = 13;
        WriteSectionHeader(ws, row++, "Resumen por tablero");
        WriteHeader(ws, row, new[]
        {
            "Tablero", "Descripcion", "Proyectados", "Circuitos", "Activos", "Reservas",
            "Distancia m", "Largo m", "Cajas", "Estado"
        });
        row++;

        foreach (var board in tableros.OrderBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase))
        {
            int circuits = board.Circuitos.Count;
            int active = board.Circuitos.Count(c => c.EsActivo);
            int reserve = board.Circuitos.Count(c => c.EsReservaEquipada);
            decimal distance = board.Circuitos.Sum(c => c.DistanciaTablero);
            decimal length = board.Circuitos.Sum(c => c.LargoTotal);
            int boxes = board.Circuitos.Sum(c => c.TotalCajas);

            ws.Cell(row, 1).Value = board.Nombre;
            ws.Cell(row, 2).Value = board.Descripcion;
            ws.Cell(row, 3).Value = board.CircuitosProyectados;
            ws.Cell(row, 4).Value = circuits;
            ws.Cell(row, 5).Value = active;
            ws.Cell(row, 6).Value = reserve;
            ws.Cell(row, 7).Value = distance;
            ws.Cell(row, 8).Value = length;
            ws.Cell(row, 9).Value = boxes;
            ws.Cell(row, 10).Value = circuits == 0 ? "Sin circuitos" : active == 0 ? "Solo reservas" : "OK";
            row++;
        }

        ApplyTableFormat(ws, 14, row - 1, 10);
        if (row > 14)
            ws.Range(14, 3, row - 1, 9).Style.NumberFormat.Format = "#,##0.###";
        ws.Column(1).Width = 18;
        ws.Column(2).Width = 28;
        ws.Column(3).Width = 12;
        ws.Column(4).Width = 12;
        ws.Column(5).Width = 10;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 12;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 10;
        ws.Column(10).Width = 16;
        ws.SheetView.FreezeRows(13);
    }

    private static void BuildBoardSheet(XLWorkbook wb, IReadOnlyList<CuadroCargaSCRow> tableros)
    {
        var ws = wb.Worksheets.Add("01_TABLEROS");
        ApplyBaseStyle(ws);

        WriteTitle(ws, "TABLEROS Y CUADROS DE CARGA");
        WriteSectionHeader(ws, 3, "Detalle tecnico por tablero");
        WriteHeader(ws, 4, new[]
        {
            "Tablero", "Descripcion", "Circuito", "Tipo", "Estado", "Sistema", "Num. cond.",
            "Merma %", "Distancia m", "Largo circuito m", "Cajas", "Artefactos"
        });

        int row = 5;
        foreach (var board in tableros.OrderBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase))
        {
            if (board.Circuitos.Count == 0)
            {
                ws.Cell(row, 1).Value = board.Nombre;
                ws.Cell(row, 2).Value = board.Descripcion;
                ws.Cell(row, 5).Value = "Sin circuitos";
                row++;
                continue;
            }

            foreach (var circuit in board.Circuitos.OrderBy(x => x.Numero))
            {
                ws.Cell(row, 1).Value = board.Nombre;
                ws.Cell(row, 2).Value = board.Descripcion;
                ws.Cell(row, 3).Value = circuit.Numero;
                ws.Cell(row, 4).Value = circuit.Tipo;
                ws.Cell(row, 5).Value = circuit.Estado;
                ws.Cell(row, 6).Value = circuit.Sistema;
                ws.Cell(row, 7).Value = circuit.NumConductores;
                ws.Cell(row, 8).Value = circuit.MermaPct;
                ws.Cell(row, 9).Value = circuit.DistanciaTablero;
                ws.Cell(row, 10).Value = circuit.LargoTotal;
                ws.Cell(row, 11).Value = circuit.TotalCajas;
                ws.Cell(row, 12).Value = circuit.Artefactos.Count == 0
                    ? "-"
                    : string.Join(", ", circuit.Artefactos.Select(a => $"{a.Nombre} x{a.Cantidad:0.##}"));
                row++;
            }
        }

        ApplyTableFormat(ws, 4, row - 1, 12);
        if (row > 5)
            ws.Range(5, 3, row - 1, 11).Style.NumberFormat.Format = "#,##0.###";
        ws.Columns(1, 2).Width = 20;
        ws.Column(3).Width = 10;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 16;
        ws.Column(7).Width = 12;
        ws.Column(8).Width = 10;
        ws.Column(9).Width = 12;
        ws.Column(10).Width = 16;
        ws.Column(11).Width = 10;
        ws.Column(12).Width = 44;
        ws.SheetView.FreezeRows(4);
    }

    private void BuildCircuitSheet(XLWorkbook wb, IReadOnlyList<CuadroCargaSCRow> tableros)
    {
        var ws = wb.Worksheets.Add("02_CIRCUITOS");
        ApplyBaseStyle(ws);

        WriteTitle(ws, "DETALLE DE CIRCUITOS");
        WriteSectionHeader(ws, 3, "Una fila por circuito C.SEC");
        WriteHeader(ws, 4, new[]
        {
            "Tablero", "Circuito", "Tipo", "Estado", "Descripcion", "Dist. tablero m", "Largo circuito m",
            "Tipo tuberia", "Seccion tuberia", "Cond. deriv.", "Seccion deriv.", "Cond. distrib.",
            "Seccion distrib.", "Num. cond.", "Largo deriv. m", "Largo distrib. m", "Largo tuberia m",
            "Cajas", "Artefactos"
        });

        int row = 5;
        foreach (var board in tableros.OrderBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase))
            foreach (var circuit in board.Circuitos.OrderBy(x => x.Numero))
            {
                ws.Cell(row, 1).Value = board.Nombre;
                ws.Cell(row, 2).Value = circuit.Numero;
                ws.Cell(row, 3).Value = circuit.Tipo;
                ws.Cell(row, 4).Value = circuit.Estado;
                ws.Cell(row, 5).Value = circuit.Descripcion;
                ws.Cell(row, 6).Value = circuit.DistanciaTablero;
                ws.Cell(row, 7).Value = circuit.LargoTotal;
                ws.Cell(row, 8).Value = circuit.TipoTuberia;
                ws.Cell(row, 9).Value = circuit.SeccionTuberia;
                ws.Cell(row, 10).Value = circuit.ConductorDerivacion;
                ws.Cell(row, 11).Value = circuit.SeccionDerivacion;
                ws.Cell(row, 12).Value = circuit.ConductorDistribucion;
                ws.Cell(row, 13).Value = circuit.SeccionDistribucion;
                ws.Cell(row, 14).Value = circuit.NumConductores;
                ws.Cell(row, 15).Value = circuit.LargoDerivacion;
                ws.Cell(row, 16).Value = circuit.LargoDistribucion;
                ws.Cell(row, 17).Value = circuit.LargoTuberia;
                ws.Cell(row, 18).Value = circuit.TotalCajas;
                ws.Cell(row, 19).Value = circuit.Artefactos.Count == 0
                    ? "-"
                    : string.Join(", ", circuit.Artefactos.Select(a => a.Nombre));
                row++;
            }

        ApplyTableFormat(ws, 4, row - 1, 19);
        if (row > 5)
        {
            ws.Range(5, 2, row - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws.Range(5, 6, row - 1, 7).Style.NumberFormat.Format = "#,##0.###";
            ws.Range(5, 14, row - 1, 18).Style.NumberFormat.Format = "#,##0.###";
        }
        ws.Column(1).Width = 18;
        ws.Column(2).Width = 10;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 34;
        ws.Column(6).Width = 12;
        ws.Column(7).Width = 12;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 12;
        ws.Column(10).Width = 14;
        ws.Column(11).Width = 12;
        ws.Column(12).Width = 14;
        ws.Column(13).Width = 14;
        ws.Column(14).Width = 10;
        ws.Column(15).Width = 12;
        ws.Column(16).Width = 12;
        ws.Column(17).Width = 12;
        ws.Column(18).Width = 8;
        ws.Column(19).Width = 34;
        ws.SheetView.FreezeRows(4);
    }

    private void BuildMaterialsSheet(XLWorkbook wb, ExportProjectContext ctx, IReadOnlyDictionary<string, CubicacionSCEngine.MaterialSC> materials, IReadOnlyDictionary<string, MaterialRow> catalog)
    {
        var ws = wb.Worksheets.Add("03_MATERIALES");
        ApplyBaseStyle(ws);

        WriteTitle(ws, "RESUMEN DE MATERIALES C.SEC");
        WriteSectionHeader(ws, 3, "Materiales agregados por el motor C.SEC");
        WriteHeader(ws, 4, new[]
        {
            "Categoria", "Material", "Unidad", "Cantidad", "UF Unit", "UF Total", "CLP Unit", "CLP Total", "Estado precio"
        });

        int row = 5;
        foreach (var item in materials
            .Select(kv => new { kv.Key, kv.Value, Category = CategorizeMaterial(kv.Key) })
            .OrderBy(x => x.Category.Order)
            .ThenByDescending(x => x.Value.Cantidad)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            catalog.TryGetValue(item.Key, out var mat);
            decimal ufUnit = mat != null ? mat.MatUf + mat.MoUf : 0m;
            decimal ufTotal = ufUnit * item.Value.Cantidad;
            decimal clpUnit = ConvertUfToCurrency(ctx, ufUnit);
            decimal clpTotal = ConvertUfToCurrency(ctx, ufTotal);

            ws.Cell(row, 1).Value = item.Category.Name;
            ws.Cell(row, 2).Value = item.Key;
            ws.Cell(row, 3).Value = item.Value.Unidad;
            ws.Cell(row, 4).Value = item.Value.Cantidad;
            ws.Cell(row, 5).Value = ufUnit;
            ws.Cell(row, 6).Value = ufTotal;
            ws.Cell(row, 7).Value = clpUnit;
            ws.Cell(row, 8).Value = clpTotal;
            ws.Cell(row, 9).Value = mat != null ? mat.EstadoPrecio : "Sin precio";
            row++;
        }

        ApplyTableFormat(ws, 4, row - 1, 9);
        if (row > 5)
            ws.Range(5, 4, row - 1, 8).Style.NumberFormat.Format = "#,##0.###";
        ws.Column(1).Width = 16;
        ws.Column(2).Width = 40;
        ws.Column(3).Width = 10;
        ws.Column(4).Width = 12;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 12;
        ws.Column(7).Width = 14;
        ws.Column(8).Width = 14;
        ws.Column(9).Width = 16;
        ws.SheetView.FreezeRows(4);
    }

    private static void BuildConfigSheet(XLWorkbook wb, CubicacionScConfig config)
    {
        var ws = wb.Worksheets.Add("04_PARAMETROS");
        ApplyBaseStyle(ws);

        WriteTitle(ws, "PARAMETROS C.SEC");
        WriteSectionHeader(ws, 3, "Sistemas");
        WriteHeader(ws, 4, new[] { "Sistema", "Conductores", "Merma %" });
        int row = 5;
        foreach (var sistema in config.Sistemas)
        {
            ws.Cell(row, 1).Value = sistema.Nombre;
            ws.Cell(row, 2).Value = sistema.NumConductores;
            ws.Cell(row, 3).Value = sistema.MermaPct;
            row++;
        }
        ApplyTableFormat(ws, 4, row - 1, 3);
        ws.Range(5, 2, row - 1, 3).Style.NumberFormat.Format = "#,##0.###";

        row += 2;
        WriteSectionHeader(ws, row++, "Flexibles por diametro");
        WriteHeader(ws, row, new[] { "Diametro", "Flexible", "Metros por caja" });
        row++;
        foreach (var flex in config.Flexibles)
        {
            ws.Cell(row, 1).Value = flex.Diametro;
            ws.Cell(row, 2).Value = flex.TipoFlexible;
            ws.Cell(row, 3).Value = flex.MetrosPorCaja;
            row++;
        }
        ApplyTableFormat(ws, row - config.Flexibles.Count - 1, row - 1, 3);
        if (config.Flexibles.Count > 0)
            ws.Range(row - config.Flexibles.Count, 3, row - 1, 3).Style.NumberFormat.Format = "#,##0.###";

        ws.Column(1).Width = 16;
        ws.Column(2).Width = 34;
        ws.Column(3).Width = 18;
        ws.SheetView.FreezeRows(4);
    }

    private static void WriteProjectHeader(IXLWorksheet ws, ExportProjectContext ctx, int row)
    {
        ws.Cell(row, 1).Value = $"Proyecto: {ctx.ProjectTitle}";
        ws.Cell(row, 4).Value = $"Cliente: {ctx.ClientName}";
        ws.Cell(row, 7).Value = $"Ubicacion: {ctx.Location}";
        ws.Cell(row + 1, 1).Value = $"Fecha: {ctx.ExportedAt:dd/MM/yyyy HH:mm}";
        ws.Cell(row + 1, 4).Value = $"Version: {ctx.VersionSummary}";
        ws.Cell(row + 1, 7).Value = $"Moneda: {ctx.CurrencyLabel}";
    }

    private static void WriteKpi(IXLWorksheet ws, int row, int column, string label, string value)
    {
        var range = ws.Range(row, column, row + 1, column + 1);
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FB");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(row, column).Value = label;
        ws.Cell(row, column).Style.Font.Bold = true;
        ws.Cell(row + 1, column).Value = value;
        ws.Cell(row + 1, column).Style.Font.Bold = true;
        ws.Cell(row + 1, column).Style.Font.FontSize = 12;
    }

    private static void WriteTitle(IXLWorksheet ws, string title)
    {
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 10).Merge();
    }

    private static void WriteSectionHeader(IXLWorksheet ws, int row, string title)
    {
        ws.Cell(row, 1).Value = title;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 11;
        ws.Range(row, 1, row, 10).Merge();
        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
    }

    private static void WriteHeader(IXLWorksheet ws, int row, IReadOnlyList<string> headers)
    {
        for (int i = 0; i < headers.Count; i++)
            ws.Cell(row, i + 1).Value = headers[i];

        var range = ws.Range(row, 1, row, headers.Count);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static void ApplyBaseStyle(IXLWorksheet ws)
    {
        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 9;
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplyTableFormat(IXLWorksheet ws, int headerRow, int lastRow, int lastColumn)
    {
        if (lastRow <= headerRow)
            return;

        var range = ws.Range(headerRow, 1, lastRow, lastColumn);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Alignment.WrapText = true;
        ws.Range(headerRow + 1, 3, lastRow, lastColumn).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private decimal ConvertUfToCurrency(ExportProjectContext ctx, decimal uf)
        => currencyConversion.ConvertUfToCurrency(uf, ctx.CurrencyLabel, ctx.Project.ValorUfClp, ctx.Project.TipoCambioUsdClp);

    private static (string Name, int Order) CategorizeMaterial(string name)
    {
        name = (name ?? string.Empty).Trim().ToUpperInvariant();
        if (name.StartsWith("TUBERIA")) return ("Canalizacion", 0);
        if (name.StartsWith("CONDUCTOR") || name.StartsWith("CINTA LED")) return ("Conductores", 1);
        if (name.StartsWith("INT") || name.StartsWith("ENCH") || name.StartsWith("SENSOR") || name.StartsWith("SEÑAL") || name.StartsWith("MONTAJE")) return ("Artefactos", 2);
        return ("Cajas", 3);
    }

    public void ExportCuadroCargasSec(ExportProjectContext context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de exportacion requerida.", nameof(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var wb = new XLWorkbook();
        var tableros = context.Data.CubicacionSC ?? new List<CuadroCargaSCRow>();
        BuildCuadroCargasSecSheet(wb, context, tableros);
        wb.SaveAs(path);
    }

    private static void BuildCuadroCargasSecSheet(XLWorkbook wb, ExportProjectContext ctx, IReadOnlyList<CuadroCargaSCRow> tableros)
    {
        var ws = wb.Worksheets.Add("05_CUADRO_CARGAS_SEC");
        ApplyBaseStyle(ws);

        WriteTitle(ws, "CUADRO DE CARGAS SEC (RIC N°02 / RIC N°03)");
        WriteProjectHeader(ws, ctx, 2);

        int row = 5;
        foreach (var board in tableros.OrderBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase))
        {
            var summary = CuadroCargasSecEngine.CalculateTableroSummary(board);

            var cardHeader = ws.Range(row, 1, row, 12);
            cardHeader.Merge();
            cardHeader.Value = $"TABLERO: {summary.TableroNombre} - {summary.Descripcion}";
            cardHeader.Style.Font.Bold = true;
            cardHeader.Style.Font.FontSize = 11;
            cardHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            cardHeader.Style.Font.FontColor = XLColor.White;
            row++;

            ws.Cell(row, 1).Value = "P. Instalada:";
            ws.Cell(row, 2).Value = $"{summary.PotenciaTotalInstaladaKw:N2} kW";
            ws.Cell(row, 3).Value = "F. Simultaneidad:";
            ws.Cell(row, 4).Value = $"{summary.FactorSimultaneidad:P0}";
            ws.Cell(row, 5).Value = "P. Demanda:";
            ws.Cell(row, 6).Value = $"{summary.PotenciaTotalDemandaKw:N2} kW";
            ws.Cell(row, 7).Value = "Fase R / S / T (kW):";
            ws.Cell(row, 8).Value = $"R={summary.PotenciaFaseRKw:N2} | S={summary.PotenciaFaseSKw:N2} | T={summary.PotenciaFaseTKw:N2}";
            ws.Cell(row, 9).Value = "% Desbalance:";
            ws.Cell(row, 10).Value = $"{summary.PorcentajeDesbalance:N1}%";
            ws.Cell(row, 11).Value = "Estado SEC:";
            ws.Cell(row, 12).Value = summary.ConformeSec ? "CONFORME (≤ 10%)" : "ALERTA DESBALANCE (> 10%)";

            var secStatusCell = ws.Cell(row, 12);
            secStatusCell.Style.Font.Bold = true;
            secStatusCell.Style.Font.FontColor = summary.ConformeSec ? XLColor.FromHtml("#15803D") : XLColor.FromHtml("#DC2626");

            var metricRange = ws.Range(row, 1, row, 12);
            metricRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            metricRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            metricRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
            row++;

            ws.Cell(row, 1).Value = "Disyuntor General:";
            ws.Cell(row, 2).Value = summary.DisyuntorGeneralSugerido;
            ws.Cell(row, 4).Value = "Diferencial General:";
            ws.Cell(row, 5).Value = summary.DiferencialGeneralSugerido;
            ws.Cell(row, 7).Value = "Alimentador Sugerido:";
            ws.Cell(row, 8).Value = summary.AlimentadorSugerido;
            ws.Cell(row, 10).Value = "Canalización:";
            ws.Cell(row, 11).Value = summary.DuctoAlimentadorSugerido;
            var techRange = ws.Range(row, 1, row, 12);
            techRange.Style.Font.Italic = true;
            techRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
            row += 2;

            int headerRow = row;
            WriteHeader(ws, row, new[]
            {
                "N°", "Descripción", "Tipo", "Sistema", "Fase", "Potencia W", "Potencia kW", "Corriente A",
                "Disyuntor", "Diferencial", "Conductor", "Canalización"
            });
            row++;

            foreach (var c in summary.Circuitos)
            {
                ws.Cell(row, 1).Value = c.Numero;
                ws.Cell(row, 2).Value = c.Descripcion;
                ws.Cell(row, 3).Value = c.Tipo;
                ws.Cell(row, 4).Value = c.Sistema;
                ws.Cell(row, 5).Value = c.Fase;
                ws.Cell(row, 6).Value = c.PotenciaW;
                ws.Cell(row, 7).Value = c.PotenciaKw;
                ws.Cell(row, 8).Value = c.CorrienteA;
                ws.Cell(row, 9).Value = c.Disyuntor;
                ws.Cell(row, 10).Value = c.Diferencial;
                ws.Cell(row, 11).Value = c.Conductor;
                ws.Cell(row, 12).Value = c.Canalizacion;
                row++;
            }

            ApplyTableFormat(ws, headerRow, row - 1, 12);
            if (row > headerRow + 1)
            {
                ws.Range(headerRow + 1, 6, row - 1, 6).Style.NumberFormat.Format = "#,##0";
                ws.Range(headerRow + 1, 7, row - 1, 7).Style.NumberFormat.Format = "#,##0.00";
                ws.Range(headerRow + 1, 8, row - 1, 8).Style.NumberFormat.Format = "#,##0.0";
            }
            row += 2;
        }

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 26;
        ws.Column(3).Width = 10;
        ws.Column(4).Width = 10;
        ws.Column(5).Width = 8;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 14;
        ws.Column(8).Width = 14;
        ws.Column(9).Width = 18;
        ws.Column(10).Width = 18;
        ws.Column(11).Width = 24;
        ws.Column(12).Width = 24;
    }
}
