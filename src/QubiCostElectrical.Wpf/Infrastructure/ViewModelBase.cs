using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QubiCostElectrical.Wpf.Infrastructure;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Parser tolerante de decimales editados a mano: acepta "," o "." como separador
    /// decimal (sin separador de miles — los campos numéricos editables de este app se muestran
    /// sin agrupar para no romper este parseo, ver ClosingPolicySectionVm/CoefFactorVm).</summary>
    protected static bool TryParseDecimal(string? text, out decimal value)
    {
        return DataGridCellAccessor.TryParseFlexibleDecimal(text, out value);

    }

    /// <summary>Clampea un porcentaje al rango [0, 100] — uso en campos editables de VM (Wpf).
    /// Equivalente de capa UI a MarkupPolicy.ClampPct (Domain); se mantiene separado a propósito
    /// para no crear una dependencia de un ViewModel hacia el motor de cálculo.</summary>
    protected static decimal ClampPct(decimal value) => System.Math.Clamp(value, 0m, 100m);
}
