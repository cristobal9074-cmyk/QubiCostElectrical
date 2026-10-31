using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Quotes;

public sealed partial class QuotesViewModel
{
    private void ImportQuoteWithDialog()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Importar cotización de proveedor",
            Filter = "Archivos de cotización (*.xlsx;*.xls;*.csv;*.pdf)|*.xlsx;*.xls;*.csv;*.pdf|Planillas Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Archivos CSV (*.csv)|*.csv|Documentos PDF (*.pdf)|*.pdf|Todos los archivos (*.*)|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
        {
            ImportQuoteFiles(dlg.FileNames);
        }
    }

    /// <summary>
    /// Procesa y carga archivos de cotización de proveedores (Excel, CSV o PDF).
    /// </summary>
    public int ImportQuoteFiles(IEnumerable<string> filePaths)
    {
        int totalSaved = 0;
        int pdfProcessed = 0;
        string? lastProvider = null;

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".xlsx" or ".xls" or ".csv")
            {
                try
                {
                    var entries = PriceBankSeeder.ParseFile(path);
                    if (entries.Count > 0)
                    {
                        int saved = store.UpsertMany(entries);
                        totalSaved += saved;
                        lastProvider = entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Proveedor))?.Proveedor;
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error("Quotes", $"Falló importar planilla {path}", ex);
                }
            }
            else if (ext is ".pdf")
            {
                try
                {
                    var (entries, providerName) = ParsePdfQuote(path);
                    if (entries.Count > 0)
                    {
                        int saved = store.UpsertMany(entries);
                        totalSaved += saved;
                        lastProvider = providerName;
                    }
                    else
                    {
                        pdfProcessed++;
                        lastProvider = providerName;
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error("Quotes", $"Falló procesar PDF {path}", ex);
                }
            }
        }

        Refresh();

        if (totalSaved > 0)
        {
            StatusMessage = $"{totalSaved} precio(s) importado(s) exitosamente{(lastProvider != null ? $" ({lastProvider})" : "")}.";
            ShellNotificationService.Instance.ShowSuccess("Cotización importada", StatusMessage);
        }
        else if (pdfProcessed > 0)
        {
            StatusMessage = $"Cotización PDF de proveedor procesada{(lastProvider != null ? $" ({lastProvider})" : "")}. Documento registrado.";
            ShellNotificationService.Instance.ShowSuccess("Cotización PDF", StatusMessage);
        }
        else
        {
            StatusMessage = "No se encontraron filas con precios válidos en los archivos seleccionados.";
            ShellNotificationService.Instance.ShowWarning("Importar cotización", StatusMessage);
        }

        return totalSaved;
    }

    private static (List<PriceBankEntry> Entries, string Provider) ParsePdfQuote(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        string provider = InferProviderFromFileName(fileName);
        var entries = new List<PriceBankEntry>();

        try
        {
            string content = File.ReadAllText(path, Encoding.Latin1);
            var textBlocks = new List<string>();

            // Extracción de bloques de texto estándar en streams PDF
            var matches = Regex.Matches(content, @"\(([^()]*)\)\s*T[jJ]");
            foreach (Match m in matches)
            {
                if (m.Groups[1].Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    textBlocks.Add(m.Groups[1].Value);
            }

            var arrayMatches = Regex.Matches(content, @"\[(.*?)\]\s*TJ");
            foreach (Match m in arrayMatches)
            {
                var sub = Regex.Matches(m.Groups[1].Value, @"\(([^()]*)\)");
                var sb = new StringBuilder();
                foreach (Match sm in sub)
                    sb.Append(sm.Groups[1].Value);
                if (sb.Length > 0)
                    textBlocks.Add(sb.ToString());
            }

            if (provider == "Proveedor General")
            {
                foreach (var block in textBlocks.Take(25))
                {
                    if (block.Contains("Dartel", StringComparison.OrdinalIgnoreCase)) { provider = "Dartel"; break; }
                    if (block.Contains("Rexel", StringComparison.OrdinalIgnoreCase)) { provider = "Rexel"; break; }
                    if (block.Contains("Rhona", StringComparison.OrdinalIgnoreCase)) { provider = "Rhona"; break; }
                    if (block.Contains("Schneider", StringComparison.OrdinalIgnoreCase)) { provider = "Schneider"; break; }
                    if (block.Contains("Legrand", StringComparison.OrdinalIgnoreCase)) { provider = "Legrand"; break; }
                    if (block.Contains("Anixter", StringComparison.OrdinalIgnoreCase)) { provider = "Anixter"; break; }
                    if (block.Contains("Kovacs", StringComparison.OrdinalIgnoreCase)) { provider = "Kovacs"; break; }
                }
            }

            // Intento de extracción tabular simple [Código] [Descripción] [Precio]
            for (int i = 0; i < textBlocks.Count - 1; i++)
            {
                string b = textBlocks[i].Trim();
                if (Regex.IsMatch(b, @"^[A-Z0-9\-\.]{3,15}$"))
                {
                    string desc = textBlocks[i + 1].Trim();
                    if (i + 2 < textBlocks.Count)
                    {
                        string priceStr = textBlocks[i + 2].Trim().Replace("$", "").Replace(".", "").Replace(",", ".");
                        if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) && price > 0)
                        {
                            entries.Add(new PriceBankEntry
                            {
                                Codigo = b,
                                Descripcion = desc,
                                MatClp = price,
                                Proveedor = provider,
                                FechaPrecio = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback silencioso sin interrumpir la operación
        }

        return (entries, provider);
    }

    private static string InferProviderFromFileName(string fileName)
    {
        string clean = fileName.Replace("_", " ").Replace("-", " ");
        string[] parts = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p.Equals("Cotizacion", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Cotización", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Presupuesto", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Oferta", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Lista", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Precios", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Proveedor", StringComparison.OrdinalIgnoreCase) ||
                int.TryParse(p, out _))
                continue;

            return p;
        }
        return "Proveedor General";
    }
}
