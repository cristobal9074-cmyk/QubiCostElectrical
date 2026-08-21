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

public sealed class CostoFamiliaVm
{
    public string Familia { get; init; } = "";
    public int MaterialesCount { get; init; }
    public decimal TotalClp { get; init; }
    public decimal TotalUf { get; init; }
    public string TotalLabel { get; init; } = "";
    public string TotalMonedaLabel { get; init; } = "";
    public string CountLabel { get; init; } = "";
}
