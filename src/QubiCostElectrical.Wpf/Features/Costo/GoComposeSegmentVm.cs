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

public sealed class GoComposeSegmentVm
{
    public System.Windows.Media.Brush Fill { get; init; } = System.Windows.Media.Brushes.Transparent;
    public double WidthUnits { get; init; }
}
