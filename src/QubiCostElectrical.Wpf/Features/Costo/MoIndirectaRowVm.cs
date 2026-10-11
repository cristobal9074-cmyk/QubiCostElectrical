using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class MoIndirectaRowVm
{
    public string Cargo { get; init; } = "";
    public string Tag { get; init; } = "Ind";
    public int Dotacion { get; init; }
    public decimal SueldoMensualClp { get; init; }
    /// <summary>Fracción del sueldo cobrada en la obra (0..1). Default 1 = 100%.</summary>
    public decimal FraccionObra { get; init; } = 1m;
    public decimal TotalAnualClp => Dotacion * SueldoMensualClp * FraccionObra;
    public decimal Pct { get; init; }

    public string DotacionLabel => Dotacion.ToString();
    public string SueldoLabel => CurrencyFormat.Clp(SueldoMensualClp);
    public string TotalLabel => CurrencyFormat.Clp(TotalAnualClp);
    public string FraccionLabel => $"{FraccionObra:N2}";
    public string PctLabel => $"{Pct:N1}";
}
