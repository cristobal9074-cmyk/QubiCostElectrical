using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed class VersionComparisonExecutionResult
{
    public BudgetVersionComparisonSummary? Summary { get; set; }
    public List<VersionComparisonRowVm> Rows { get; } = new();
    public string SummaryText { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string TotalsText { get; set; } = "—";
    public string AddedText { get; set; } = "0 nuevas";
    public string ChangedText { get; set; } = "0 modificadas";
    public string RemovedText { get; set; } = "0 eliminadas";
    public string UnchangedText { get; set; } = "0 sin cambio";
    public string AdicionalesText { get; set; } = "";
    public string DeductivosText { get; set; } = "";
    public string NetVariationText { get; set; } = "";
    public decimal DeltaUf { get; set; }
    public decimal DeltaPct { get; set; }
    public string ModeHint { get; set; } = "";
    public IReadOnlyList<ItemizadoRow>? IncomingAddendaItems { get; set; }
    public string AddendaFileName { get; set; } = "";
    public bool IsAddenda { get; set; }
}

/// <summary>
/// Coordina la comparación de revisiones históricas, archivos externos y planillas Excel de Addenda,
/// calculando los desgloses del semáforo y deltas monetarios.
/// </summary>
public sealed class VersionsComparisonCoordinator
{
    private readonly IVersionComparisonService versionComparison;
    private readonly IItemizadoSpreadsheetReader? spreadsheetReader;
    private readonly ImportarItemizadoUseCase? importItemizadoUseCase;
    private readonly IFileDialogService? fileDialog;

    public VersionsComparisonCoordinator(
        IVersionComparisonService versionComparison,
        IItemizadoSpreadsheetReader? spreadsheetReader = null,
        ImportarItemizadoUseCase? importItemizadoUseCase = null,
        IFileDialogService? fileDialog = null)
    {
        this.versionComparison = versionComparison;
        this.spreadsheetReader = spreadsheetReader;
        this.importItemizadoUseCase = importItemizadoUseCase;
        this.fileDialog = fileDialog;
    }

    public string BuildTotalsText(BudgetVersionComparisonSummary summary, string currentVerb, bool muestraUf) => muestraUf
        ? $"Base {summary.BaselineUf:N3} UF · {currentVerb} {summary.CurrentUf:N3} UF · Delta {summary.DeltaUf:N3} UF ({summary.DeltaPct:N2}%)"
        : $"Base $ {summary.BaselineClp:N0} · {currentVerb} $ {summary.CurrentClp:N0} · Delta $ {summary.DeltaClp:N0} ({summary.DeltaPct:N2}%)";

    public (string Adicionales, string Deductivos, string Neto) BuildVariationTexts(
        BudgetVersionComparisonSummary summary, bool muestraUf)
    {
        if (!muestraUf)
        {
            return (
                $"+$ {Math.Abs(summary.TotalAdicionalesClp):N0}",
                $"- $ {Math.Abs(summary.TotalDeductivosClp):N0}",
                $"{(summary.NetVariationClp >= 0m ? "+" : "-")}$ {Math.Abs(summary.NetVariationClp):N0}");
        }

        return (
            $"+{summary.TotalAdicionalesUf:N3} UF",
            $"{summary.TotalDeductivosUf:N3} UF",
            $"{(summary.NetVariationUf >= 0m ? "+" : "")}{summary.NetVariationUf:N3} UF");
    }

    public VersionComparisonExecutionResult CompareInternal(
        VersionRowVm? selectedVersion,
        bool compareAgainstCurrent,
        VersionRowVm? selectedComparisonVersion,
        BudgetProjectSession session,
        bool muestraUf,
        VersionsCurrencyFormat currencyFormat)
    {
        var res = new VersionComparisonExecutionResult();

        var selected = selectedVersion?.Source;
        if (selected == null)
        {
            res.SummaryText = "Sin comparación";
            res.StatusText = "Selecciona una versión para comparar con el estado actual.";
            res.AdicionalesText = muestraUf ? "+0.000 UF" : "+$ 0";
            res.DeductivosText = muestraUf ? "0.000 UF" : "- $ 0";
            res.NetVariationText = muestraUf ? "0.000 UF" : "$ 0";
            res.ModeHint = "Selecciona una versión para comparar.";
            return res;
        }

        string baselineLabel = $"{selected.Codigo} · {selected.Nombre}";
        BudgetVersionComparisonSummary summary;

        if (compareAgainstCurrent)
        {
            summary = versionComparison.Compare(
                selected.Snapshot,
                session.CreateSnapshot(),
                baselineLabel,
                "Estado actual");
        }
        else
        {
            var comparison = selectedComparisonVersion?.Source;
            if (comparison == null)
            {
                res.SummaryText = "Sin comparación";
                res.StatusText = "Selecciona una segunda versión para comparar.";
                res.AdicionalesText = muestraUf ? "+0.000 UF" : "+$ 0";
                res.DeductivosText = muestraUf ? "0.000 UF" : "- $ 0";
                res.NetVariationText = muestraUf ? "0.000 UF" : "$ 0";
                res.ModeHint = "Selecciona la versión B para comparar A vs B.";
                return res;
            }

            summary = versionComparison.Compare(
                selected.Snapshot,
                comparison.Snapshot,
                baselineLabel,
                $"{comparison.Codigo} · {comparison.Nombre}");
        }

        res.Summary = summary;
        foreach (var row in summary.Rows)
            res.Rows.Add(new VersionComparisonRowVm(row, currencyFormat));

        res.SummaryText = $"{summary.Rows.Count:N0} partidas comparadas";
        res.StatusText = summary.Rows.Count == 0
            ? "No hay diferencias entre la versión seleccionada y el estado actual."
            : $"Comparando {summary.BaselineLabel} contra {summary.CurrentLabel}.";
        res.TotalsText = BuildTotalsText(summary, "Actual", muestraUf);
        res.AddedText = $"{summary.AddedCount:N0} nuevas";
        res.ChangedText = $"{summary.ChangedCount:N0} modificadas";
        res.RemovedText = $"{summary.RemovedCount:N0} eliminadas";
        res.UnchangedText = $"{summary.UnchangedCount:N0} sin cambio";
        (res.AdicionalesText, res.DeductivosText, res.NetVariationText) = BuildVariationTexts(summary, muestraUf);
        res.DeltaUf = summary.DeltaUf;
        res.DeltaPct = summary.DeltaPct;
        res.ModeHint = compareAgainstCurrent
            ? $"Versión A = {summary.BaselineLabel} · Versión B = estado actual"
            : $"Versión A = {summary.BaselineLabel} · Versión B = {summary.CurrentLabel}";

        return res;
    }

    public VersionComparisonExecutionResult? CompareWithExcel(
        BudgetProjectSession session,
        Func<string?> promptOpenFileDirect,
        bool muestraUf,
        VersionsCurrencyFormat currencyFormat)
    {
        string? filePath = fileDialog?.PromptOpenFile("Libros de Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Todos los archivos (*.*)|*.*")
            ?? promptOpenFileDirect();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var reader = spreadsheetReader ?? new ItemizadoSpreadsheetReader();
            var parser = importItemizadoUseCase ?? new ImportarItemizadoUseCase();

            var table = reader.ReadFirstWorksheet(filePath);
            if (table == null || table.Count == 0)
            {
                WpfDialogService.InfoModal("Archivo sin datos", "El archivo seleccionado no contiene filas o datos legibles.");
                return null;
            }

            var result = parser.Execute(table.Select(r => r.ToList()).ToList());
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                WpfDialogService.InfoModal("Error al importar Addenda", $"No se pudo leer la estructura del itemizado:\n{result.Error}");
                return null;
            }

            if (result.Rows.Count == 0)
            {
                WpfDialogService.InfoModal("Sin partidas", "No se encontraron partidas válidas en el archivo de la Addenda.");
                return null;
            }

            string addendaName = Path.GetFileName(filePath);
            var snapshot = session.CreateSnapshot();
            var summary = versionComparison.CompareWithIncomingItemizado(
                result.Rows,
                snapshot,
                incomingLabel: $"Addenda ({addendaName})",
                currentLabel: "Presupuesto Base");

            var res = new VersionComparisonExecutionResult
            {
                Summary = summary,
                IncomingAddendaItems = result.Rows,
                AddendaFileName = addendaName,
                IsAddenda = true,
                SummaryText = $"{summary.Rows.Count:N0} partidas analizadas",
                StatusText = $"Semáforo de Aclaración / Addenda: {summary.BaselineLabel} vs {summary.CurrentLabel}",
                TotalsText = BuildTotalsText(summary, "Addenda", muestraUf),
                AddedText = $"{summary.AddedCount:N0} adicionales",
                ChangedText = $"{summary.ChangedCount:N0} con cambio de metraje",
                RemovedText = $"{summary.RemovedCount:N0} deductivos",
                UnchangedText = $"{summary.UnchangedCount:N0} sin cambio",
                DeltaUf = summary.DeltaUf,
                DeltaPct = summary.DeltaPct,
                ModeHint = $"Addenda entrante: «{addendaName}» · Semáforo activado"
            };

            foreach (var row in summary.Rows)
                res.Rows.Add(new VersionComparisonRowVm(row, currencyFormat));

            (res.AdicionalesText, res.DeductivosText, res.NetVariationText) = BuildVariationTexts(summary, muestraUf);

            int mayoresMetrajes = summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaCantidad > 0);
            int menoresMetrajes = summary.Rows.Count(r => r.DiffKind == RevisionDiffKind.Modificado && r.DeltaCantidad < 0);

            WpfDialogService.Info(
                "Addenda Analizada con Éxito",
                $"Se ha cargado y contrastado «{addendaName}» contra el presupuesto activo:\n\n" +
                $"• 🟢 Partidas Nuevas / Adicionales: {summary.AddedCount} partidas\n" +
                $"• 🟢 Aumentos de Metraje: {mayoresMetrajes} partidas\n" +
                $"• 🔴 Deductivos / Omitidas: {summary.RemovedCount} partidas\n" +
                $"• 🔴 Reducciones de Metraje: {menoresMetrajes} partidas\n" +
                $"• ⚡ Saldo Neto Variación: {currencyFormat.FromUfSigned(summary.NetVariationUf)} ({(summary.NetVariationClp >= 0 ? "+$ " : "$ ")}{summary.NetVariationClp:N0})\n\n" +
                $"Use los filtros del semáforo para auditar los cambios o presione «⚡ Aplicar Cantidades de Addenda» para incorporar las modificaciones al proyecto.");

            return res;
        }
        catch (Exception ex)
        {
            WpfDialogService.ErrorModal("Error al procesar Addenda", $"Ocurrió una excepción al leer o comparar la Addenda:\n{ex.Message}");
            return null;
        }
    }
}
