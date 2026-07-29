using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionRightPanelVm : ViewModelBase
{
    private CubicacionRowVm? currentRow;
    private MaterialRow? linkedMaterial;
    private string comentario = "";
    private string zona = "General";
    private string redondeoModo = "Ninguno";
    private decimal redondeoMultiplo = 0m;
    private decimal mermaPctOverride = -1m;
    private decimal herramientasPctOverride = -1m;
    private string activeTab = "Detalle";
    private List<string> alertas = new();

    private readonly BudgetProjectSession session;
    private readonly CubicacionViewModel parent;
    private readonly List<ZoneDistributionItem> allZonesMaster = new();
}
