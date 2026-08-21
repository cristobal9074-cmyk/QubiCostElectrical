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

public sealed partial class CubicacionScViewModel : SessionAwareViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly BudgetProjectSession session;
    private CuadroScRowVm? selectedCuadro;
    private CircuitoScRowVm? selectedCircuito;
    private CircuitoSCRow? clipboardCircuito;
    private bool showEmptyState = true;
    private string statusMessage = "";
    private string nuevoArtefactoNombre = "";
    private decimal nuevoArtefactoCantidad = 1;
}
