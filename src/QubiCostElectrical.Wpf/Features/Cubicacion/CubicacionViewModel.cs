using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel : ViewModelBase, IRefreshablePage, IPageWithPendingChanges, IDisposable
{
    internal const int MinVisibleRows = 8;

    private readonly BudgetProjectSession session;
    private readonly ICubicacionService cubSvc;
    private readonly ICubicacionTakeoffQueryService querySvc;
    private readonly IProjectRepository repo;
    private readonly IShellNavigationHost shell;
    private readonly IMaterialSearchService materialSearch;
    private readonly IPriceBankStore priceBankStore;
    private readonly WpfAppHost host;
    private readonly CubicacionPresenter cubicacionPresenter;
    private readonly ConjuntoElectricoService conjuntoService;
    private readonly CubicacionTotalsCalculator totalsCalculator;
    private readonly CubicacionAutocompleteService autocompleteService;
    private readonly CubicacionSessionCoordinator sessionCoordinator;
    private readonly CubicacionPersistenceService persistenceService;
    private readonly CubicacionLoadingService loadingService;
    private readonly CubicacionSectorService sectorService;
    private readonly CubicacionRowMutationService rowMutationService;
    private readonly CubicacionClipboardCoordinator clipboardCoordinator;

    // MarkDirty("cubicacion") dispara session.Changed de forma SÍNCRONA, y este mismo
    // ViewModel escucha ese evento para recargar Rows/Partidas desde session (necesario
    // cuando el cambio viene de OTRA pantalla, p. ej. CubicacionSc). El problema: cualquier
    // edición hecha AQUÍ (tipear un código, aplicar material, pegar) ya actualiza Rows en
    // memoria, así que ese recargo es un eco de su propio cambio — antes pasaba invisible
    // en el mismo tick de UI, pero forzaba un ciclo completo de Partidas+Rows en cada
    // pulsación de tecla. Este contador suprime el eco sin afectar recargas genuinas
    // originadas fuera de esta instancia (Undo/Redo, importación, otra pantalla).
    private int suppressSessionReload;
}
