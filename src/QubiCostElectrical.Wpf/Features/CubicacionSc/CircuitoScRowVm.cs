using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed partial class CircuitoScRowVm : ViewModelBase
{
    internal CircuitoSCRow Source { get; }
    private readonly CubicacionScConfig config;
    private readonly Action? onEstadoChanged;
    private readonly Action? onPisosChanged;
    private readonly Func<bool>? canEdit;
    private readonly Action? onChanged;
    private readonly IReadOnlyList<ZonaProyecto> zonas;
}
