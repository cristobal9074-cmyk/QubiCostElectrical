using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public sealed class ChangeOrderConsolidatedMaterialRow
{
    public string Tipo { get; set; } = TipoRecurso.MAT;
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "UN";
    public decimal CantidadTotal { get; set; }
    public decimal SubtotalClp { get; set; }
    public decimal SubtotalUf { get; set; }
    public string SubtotalDisplay { get; set; } = "";
}

public partial class ChangeOrderMaterialsSummaryDialog : Window
{
    public ChangeOrderMaterialsSummaryDialog(
        IEnumerable<ChangeOrderLineVm> lineas,
        bool muestraUf,
        decimal ufRate)
    {
        InitializeComponent();
        Owner = Application.Current?.MainWindow;

        var rate = ufRate > 0m ? ufRate : PriceConversion.DefaultValorUfClp;

        var allMaterials = (lineas ?? Enumerable.Empty<ChangeOrderLineVm>())
            .SelectMany(l => l.Materiales ?? Enumerable.Empty<CubicacionRow>())
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo) || !string.IsNullOrWhiteSpace(m.Descripcion))
            .ToList();

        var grouped = allMaterials
            .GroupBy(m => new { Codigo = (m.Codigo ?? "").Trim(), Desc = (m.Descripcion ?? "").Trim(), Unidad = (m.Unidad ?? "UN").Trim(), Tipo = (m.Tipo ?? TipoRecurso.MAT).Trim() })
            .Select(g =>
            {
                decimal cantTot = g.Sum(x => x.Cantidad);
                decimal subClp = g.Sum(x => Math.Round(x.Cantidad * x.MatClpConFallback, 0, MidpointRounding.AwayFromZero) + Math.Round(x.Cantidad * x.MoClpConFallback, 0, MidpointRounding.AwayFromZero));
                decimal subUf = rate > 0m ? PriceConversion.ClpToUf(subClp, rate) : 0m;
                return new ChangeOrderConsolidatedMaterialRow
                {
                    Tipo = g.Key.Tipo,
                    Codigo = g.Key.Codigo,
                    Descripcion = g.Key.Desc,
                    Unidad = g.Key.Unidad,
                    CantidadTotal = cantTot,
                    SubtotalClp = subClp,
                    SubtotalUf = subUf,
                    SubtotalDisplay = muestraUf ? $"{subUf:N3} UF" : $"$ {subClp:N0}"
                };
            })
            .OrderBy(r => r.Codigo)
            .ToList();

        ConsolidadoGrid.ItemsSource = grouped;
        CountBadgeText.Text = $"{grouped.Count} recursos consolidados";

        decimal totalDirectoClp = grouped.Sum(g => g.SubtotalClp);
        decimal totalDirectoUf = rate > 0m ? PriceConversion.ClpToUf(totalDirectoClp, rate) : 0m;

        if (muestraUf)
        {
            TotalMatText.Text = $"{totalDirectoUf:N3} UF";
            TotalGeneralText.Text = $"{totalDirectoUf:N3} UF";
        }
        else
        {
            TotalMatText.Text = $"$ {totalDirectoClp:N0}";
            TotalGeneralText.Text = $"$ {totalDirectoClp:N0}";
        }
    }

    public static void Show(
        Window? owner,
        IEnumerable<ChangeOrderLineVm> lineas,
        bool muestraUf,
        decimal ufRate)
    {
        var dlg = new ChangeOrderMaterialsSummaryDialog(lineas, muestraUf, ufRate);
        if (owner != null) dlg.Owner = owner;
        dlg.ShowDialog();
    }
}
