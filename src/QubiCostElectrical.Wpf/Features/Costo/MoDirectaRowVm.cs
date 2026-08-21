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

public sealed class MoDirectaRowVm
{
    public string Cargo { get; init; } = "";
    public string Tag { get; init; } = "Dir";
    public int Dotacion { get; init; }
    public decimal SueldoMensualClp { get; init; }
    public decimal TotalAnualClp => Dotacion * SueldoMensualClp;
    public decimal Pct { get; init; }

    public string DotacionLabel => Dotacion + " contratos";
    public string SueldoLabel => $"$ {SueldoMensualClp:N0}";
    public string TotalLabel => $"$ {TotalAnualClp:N0}";
    public string PctLabel => $"{Pct:N1}";
}
