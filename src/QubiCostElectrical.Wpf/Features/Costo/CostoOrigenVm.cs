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

public sealed class CostoOrigenVm
{
    public string Origen { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public decimal MaterialesUf { get; init; }
    public decimal ManoObraUf { get; init; }
    public decimal EquiposUf { get; init; }
    public decimal TotalUf { get; init; }
}
