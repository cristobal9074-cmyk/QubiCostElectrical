using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Generador de Órdenes de Cambio a partir de las variaciones detectadas
/// en el comparador de versiones de presupuestos.
/// </summary>
public static class ChangeOrderDiffGenerator
{
    public static string GenerateNextNumber(IEnumerable<ChangeOrderRow> existingOrders)
    {
        int max = 0;
        if (existingOrders != null)
        {
            foreach (var oc in existingOrders)
            {
                if (oc.Numero != null && oc.Numero.StartsWith("OC-") &&
                    int.TryParse(oc.Numero[3..], out int n) && n > max)
                {
                    max = n;
                }
            }
        }
        return $"OC-{max + 1:D3}";
    }

    public static ChangeOrderRow? CreateFromDiff(
        BudgetVersionComparisonSummary summary,
        IEnumerable<ChangeOrderRow> existingOrders,
        decimal gastosGeneralesPct,
        decimal utilidadPct,
        string usuarioNombre)
    {
        if (summary == null) return null;

        var changedRows = summary.Rows
            .Where(r => r.DiffKind != RevisionDiffKind.SinCambio)
            .ToList();

        if (changedRows.Count == 0) return null;

        string nextNumero = GenerateNextNumber(existingOrders);
        string titulo = $"Ajuste contractual {summary.BaselineLabel} vs {summary.CurrentLabel}";

        decimal deltaCostoUf = summary.DeltaUf;
        decimal deltaGgUf = Math.Round(deltaCostoUf * (gastosGeneralesPct / 100m), 4);
        decimal deltaUtilUf = Math.Round(deltaCostoUf * (utilidadPct / 100m), 4);
        decimal deltaVentaUf = Math.Round(deltaCostoUf * (1m + (gastosGeneralesPct + utilidadPct) / 100m), 4);

        string author = string.IsNullOrWhiteSpace(usuarioNombre) ? "Estimador" : usuarioNombre.Trim();

        var changeOrder = new ChangeOrderRow
        {
            Numero = nextNumero,
            Titulo = titulo,
            Estado = "Borrador",
            FechaCreacion = AppClock.Now,
            FechaModificacion = AppClock.Now,
            CreadoPor = author,
            PresupuestoBaseEstado = summary.BaselineLabel,
            PresupuestoBaseFecha = AppClock.Now,
            CostoBaseUf = summary.BaselineUf,
            GastosGeneralesPct = gastosGeneralesPct,
            UtilidadPct = utilidadPct,
            DeltaCostoUf = deltaCostoUf,
            DeltaGastosGeneralesUf = deltaGgUf,
            DeltaUtilidadUf = deltaUtilUf,
            DeltaVentaUf = deltaVentaUf,
            Nota = $"Generada automáticamente desde el comparador de versiones ({summary.BaselineLabel} vs {summary.CurrentLabel}). Variación neta directa: {summary.DeltaUf:+0.###;-0.###;0} UF ($ {summary.DeltaClp:+0,0;-0,0;0} CLP)."
        };

        foreach (var r in changedRows)
        {
            string tipoCambio = r.DiffKind switch
            {
                RevisionDiffKind.Adicional => "Adicional",
                RevisionDiffKind.Deductivo => "Deductivo",
                _ => "Modificación"
            };

            changeOrder.Lineas.Add(new ChangeOrderLineRow
            {
                ItemId = r.ItemId,
                Item = r.Item,
                DescripcionPartida = r.Descripcion,
                TipoCambio = tipoCambio,
                CantidadBase = r.CantidadBase,
                CantidadNueva = r.CantidadActual,
                CostoBaseUf = r.BaselineUf,
                CostoNuevoUf = r.CurrentUf,
                DeltaCostoUf = r.DeltaUf,
                Nota = string.IsNullOrWhiteSpace(r.DetalleCambio) ? tipoCambio : r.DetalleCambio
            });
        }

        changeOrder.Historial.Add(new ChangeOrderHistoryRow
        {
            EstadoAnterior = "",
            EstadoNuevo = "Borrador",
            Fecha = AppClock.Now,
            Usuario = author,
            Nota = $"Creación automática desde Comparador de Versiones ({changedRows.Count} partida(s) con variaciones)"
        });

        return changeOrder;
    }
}
