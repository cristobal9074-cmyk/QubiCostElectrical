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

public sealed partial class CubicacionScViewModel : SessionAwareViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    private readonly IShellNavigationHost shell;
    private readonly IProjectWorkspaceService workspace;
    private readonly WpfUndoService undo;
    private readonly BudgetProjectSession session;
    private readonly ICubicacionSCReportExcelExporter cubScExcel;
    private CuadroScRowVm? selectedCuadro;
    private CircuitoScRowVm? selectedCircuito;
    private CircuitoSCRow? clipboardCircuito;
    private bool showEmptyState = true;
    private bool isLoading;
    private string statusMessage = "";
    private string nuevoArtefactoNombre = "";
    private decimal nuevoArtefactoCantidad = 1;
    private bool pendingUndoForEdit;
    /// <summary>Snapshot profundo de cuadros SC al Load/Refresh/Apply; restaura en Discard.</summary>
    private List<CuadroCargaSCRow>? baselineCuadros;
    /// <summary>Snapshot profundo del catálogo SC al Load/Refresh/Apply; restaura en Discard.</summary>
    private CubicacionScConfig? baselineConfig;
    private bool suppressSessionReload;
    private bool isAdvancedMode;
    private CubScPerspective currentPerspective = CubScPerspective.Generales;
    public Func<string, bool> ConfirmDelete { get; set; } = msg => WpfDialogService.Confirm("Eliminar tablero", msg, isWarning: true);
    public Func<string, bool> ConfirmDeleteCircuito { get; set; } = msg => WpfDialogService.Confirm("Eliminar circuito", msg, isWarning: true);
}

public enum CubScPerspective
{
    Generales,
    Deptos
}
