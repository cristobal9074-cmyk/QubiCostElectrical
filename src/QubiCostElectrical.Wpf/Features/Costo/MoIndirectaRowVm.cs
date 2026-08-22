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
    public decimal TotalAnualClp => Dotacion * SueldoMensualClp;
    public decimal Pct { get; init; }

    public string DotacionLabel => Dotacion.ToString();
    public string SueldoLabel => CurrencyFormat.Clp(SueldoMensualClp);
    public string TotalLabel => CurrencyFormat.Clp(TotalAnualClp);
    public string PctLabel => $"{Pct:N1}";
}
