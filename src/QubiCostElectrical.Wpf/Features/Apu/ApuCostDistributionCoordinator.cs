using System;
using System.Windows;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Apu;

/// <summary>
/// Coordina el desglose proporcional de costos en el APU (Materiales, Mano de Obra,
/// Equipos, Subcontratos y Otros) para alimentar la barra segmentada continua (Cost Breakdown Strip).
/// </summary>
public sealed class ApuCostDistributionCoordinator : ViewModelBase
{
    private double matPct, moPct, eqPct, scPct, otrosPct;
    private decimal costoMat, costoMo, costoEq, costoSc, costoOtros, costoDirecto;

    public double MatPct => matPct;
    public double MoPct => moPct;
    public double EqPct => eqPct;
    public double ScPct => scPct;
    public double OtrosPct => otrosPct;

    public GridLength MatWidth => new(Math.Max(0.0001, matPct), GridUnitType.Star);
    public GridLength MoWidth => new(Math.Max(0.0001, moPct), GridUnitType.Star);
    public GridLength EqWidth => new(Math.Max(0.0001, eqPct), GridUnitType.Star);
    public GridLength ScWidth => new(Math.Max(0.0001, scPct), GridUnitType.Star);
    public bool HasCost => costoDirecto > 0m && (matPct + moPct + eqPct + scPct + otrosPct) > 0.01;
    public bool HasMat => matPct > 0.05;
    public bool HasMo => moPct > 0.05;
    public bool HasEq => eqPct > 0.05;
    public bool HasSc => scPct > 0.05;
    public bool HasOtros => otrosPct > 0.05;

    public string MatTooltip => $"Materiales: {matPct:N1}% ({costoMat:N4} UF) — Suministros e insumos de la partida";
    public string MoTooltip => $"Mano de Obra: {moPct:N1}% ({costoMo:N4} UF) — Horas hombre e instalación eléctrica";
    public string EqTooltip => $"Equipos y Maquinaria: {eqPct:N1}% ({costoEq:N4} UF) — Equipamiento e instrumentos";
    public string ScTooltip => $"Subcontratos: {scPct:N1}% ({costoSc:N4} UF) — Servicios especializados y pruebas";
    public string OtrosTooltip => $"Transporte y Otros: {otrosPct:N1}% ({costoOtros:N4} UF) — Fletes y gastos auxiliares";

    public string MatBadge => $"Materiales {matPct:N1}%";
    public string MoBadge => $"Mano de Obra {moPct:N1}%";
    public string EqBadge => $"Equipos {eqPct:N1}%";
    public string ScBadge => $"Subcontratos {scPct:N1}%";
    public string OtrosBadge => $"Otros {otrosPct:N1}%";

    public void Recalculate(decimal mat, decimal mo, decimal eq, decimal sc, decimal otros, decimal directo)
    {
        costoMat = mat;
        costoMo = mo;
        costoEq = eq;
        costoSc = sc;
        costoOtros = otros;
        costoDirecto = directo;

        if (directo <= 0m)
        {
            matPct = moPct = eqPct = scPct = otrosPct = 0;
        }
        else
        {
            matPct = Math.Clamp((double)(mat / directo * 100m), 0, 100);
            moPct = Math.Clamp((double)(mo / directo * 100m), 0, 100);
            eqPct = Math.Clamp((double)(eq / directo * 100m), 0, 100);
            scPct = Math.Clamp((double)(sc / directo * 100m), 0, 100);
            otrosPct = Math.Clamp((double)(otros / directo * 100m), 0, 100);
        }

        OnPropertyChanged(string.Empty);
    }
}
