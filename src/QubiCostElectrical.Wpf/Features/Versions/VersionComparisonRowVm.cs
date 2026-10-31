using System;
using System.Collections.Generic;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed class VersionComparisonRowVm
{
    private readonly VersionsCurrencyFormat? currency;

    public VersionComparisonRowVm(BudgetVersionComparisonRow source, VersionsCurrencyFormat? currency = null)
    {
        Source = source;
        this.currency = currency;
    }

    public BudgetVersionComparisonRow Source { get; }
    public string ItemId => Source.ItemId;
    public string Item => Source.Item;
    public string Descripcion => Source.Descripcion;
    public decimal BaselineUf => Source.BaselineUf;
    public decimal CurrentUf => Source.CurrentUf;
    public decimal DeltaUf => Source.DeltaUf;
    public decimal DeltaPct => Source.DeltaPct;
    public string Estado => Source.Estado;

    public string Unidad => Source.Unidad;
    public decimal CantidadBase => Source.CantidadBase;
    public decimal CantidadActual => Source.CantidadActual;
    public decimal DeltaCantidad => Source.DeltaCantidad;
    public string DeltaCantidadText => DeltaCantidad == 0m ? "—" : (DeltaCantidad > 0m ? $"+{DeltaCantidad:N0}" : $"{DeltaCantidad:N0}");

    public decimal PuUfBase => Source.PuUfBase;
    public decimal PuUfActual => Source.PuUfActual;
    public decimal DeltaPuUf => Source.DeltaPuUf;
    public string DeltaPuUfText => DeltaPuUf == 0m ? "—" : (DeltaPuUf > 0m ? $"+{DeltaPuUf:N3}" : $"{DeltaPuUf:N3}");

    // Baseline/Current/Delta y P.U. vienen en UF reales del motor (CubicacionRow.UfTotal).
    // Con CLP se presentan en pesos y la UF desaparece de la grilla.
    public string PuBaseText => currency is null ? $"{PuUfBase:N3}" : currency.FromUf(PuUfBase);
    public string PuActualText => currency is null ? $"{PuUfActual:N3}" : currency.FromUf(PuUfActual);
    public string TotalBaseText => currency is null ? $"{BaselineUf:N3}" : currency.FromUf(BaselineUf);
    public string TotalActualText => currency is null ? $"{CurrentUf:N3}" : currency.FromUf(CurrentUf);
    public string DeltaUfText => currency is null
        ? $"{DeltaUf:N3}"
        : currency.FromUfSigned(DeltaUf);

    public decimal BaselineClp => Source.BaselineClp;
    public decimal CurrentClp => Source.CurrentClp;
    public decimal DeltaClp => Source.DeltaClp;
    public string DeltaClpText => DeltaClp == 0m ? "—" : (DeltaClp > 0m ? $"+$ {DeltaClp:N0}" : $"$ {DeltaClp:N0}");

    public RevisionDiffKind DiffKind => Source.DiffKind;
    public string DisplayBadge => Source.DisplayBadge;
    public string DetalleCambio => Source.DetalleCambio;
    public IReadOnlyList<BudgetVersionResourceDiffRow> ResourceDiffs => Source.ResourceDiffs;
    public bool HasResourceDiffs => ResourceDiffs != null && ResourceDiffs.Count > 0;

    // ═══ Propiedades dedicadas para vista comparativa Izquierda (Versión A) vs Derecha (Versión B) ═══

    public string ItemA => DiffKind == RevisionDiffKind.Adicional ? "—" : Item;
    public string DescripcionA => DiffKind == RevisionDiffKind.Adicional ? "— (Partida nueva en Versión B)" : Descripcion;
    public string UnidadA => DiffKind == RevisionDiffKind.Adicional ? "—" : (string.IsNullOrWhiteSpace(Unidad) ? "UN" : Unidad);
    public string CantidadBaseDisplay => DiffKind == RevisionDiffKind.Adicional ? "—" : $"{CantidadBase:N0} {Unidad}";
    public string CantidadBaseNumDisplay => DiffKind == RevisionDiffKind.Adicional ? "—" : $"{CantidadBase:N0}";
    public string TotalBaseDisplay => DiffKind == RevisionDiffKind.Adicional ? "—" : TotalBaseText;

    public string ItemB => DiffKind == RevisionDiffKind.Deductivo ? "—" : Item;
    public string DescripcionB => DiffKind == RevisionDiffKind.Deductivo ? "— (Partida eliminada en Versión B)" : Descripcion;
    public string UnidadB => DiffKind == RevisionDiffKind.Deductivo ? "—" : (string.IsNullOrWhiteSpace(Unidad) ? "UN" : Unidad);
    public string CantidadActualDisplay => DiffKind == RevisionDiffKind.Deductivo ? "—" : $"{CantidadActual:N0} {Unidad}";
    public string CantidadActualNumDisplay => DiffKind == RevisionDiffKind.Deductivo ? "—" : $"{CantidadActual:N0}";
    public string TotalActualDisplay => DiffKind == RevisionDiffKind.Deductivo ? "—" : TotalActualText;

    public string DeltaMontoDisplay => currency is not null && !currency.MuestraUf ? DeltaClpText : DeltaUfText;
    public string DeltaCantidadDisplay => DeltaCantidad == 0m ? "—" : (DeltaCantidad > 0m ? $"+{DeltaCantidad:N0} {Unidad}" : $"{DeltaCantidad:N0} {Unidad}");
    public string DeltaCantidadNumDisplay => DeltaCantidad == 0m ? "—" : (DeltaCantidad > 0m ? $"+{DeltaCantidad:N0}" : $"{DeltaCantidad:N0}");

    public string DeltaTotalText => DeltaMontoDisplay;
    public decimal TotalBaseUf => BaselineUf;
    public decimal TotalActualUf => CurrentUf;

    public string DiffStatusBadgeText => DiffKind switch
    {
        RevisionDiffKind.Adicional => "+ AGREGADA",
        RevisionDiffKind.Deductivo => "- ELIMINADA",
        RevisionDiffKind.Modificado => DeltaPuUf != 0m ? "Δ PRECIO" : "Δ CANTIDAD",
        _ => "SIN CAMBIO"
    };

    public bool HasDeltaPu => DeltaPuUf != 0m;

    public string DeltaPuDisplay
    {
        get
        {
            if (DeltaPuUf == 0m) return "";
            if (currency is not null && !currency.MuestraUf)
            {
                var deltaClp = Math.Round(DeltaPuUf * currency.UfRate, 0, MidpointRounding.AwayFromZero);
                return deltaClp > 0m ? $"+$ {deltaClp:N0}/u" : $"$ {deltaClp:N0}/u";
            }
            return DeltaPuUf > 0m ? $"+{DeltaPuUf:N3} UF/u" : $"{DeltaPuUf:N3} UF/u";
        }
    }

    public bool IsAdicional => DiffKind == RevisionDiffKind.Adicional;
    public bool IsDeductivo => DiffKind == RevisionDiffKind.Deductivo;
    public bool IsModificado => DiffKind == RevisionDiffKind.Modificado;
    public bool IsSinCambio => DiffKind == RevisionDiffKind.SinCambio;
}
