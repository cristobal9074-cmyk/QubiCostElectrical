using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Apu;

public sealed partial class ApuWorkspaceVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IApuService apuSvc;
    private readonly ConjuntoElectricoService conjuntoSvc = new();
    private readonly IApuTemplateStore? templateStore;
    private readonly WpfAppHost host;
    private string templateStoreStatusText = "";

    private readonly List<ApuPartidaVm> allPartidas = new();
    private ApuPartidaVm? selectedPartida;
    private ApuRecursoVm? selectedRecurso;
    private ApuRow? currentApu;
    private string partidaFilter = "";
    private bool showEmptyState = true;
    private bool hasPendingChanges;
    private bool isLoading;

    // Resumen del APU seleccionado
    private decimal costoMat;
    private decimal costoMo;
    private decimal costoEquipo;
    private decimal costoSc;
    private decimal costoHerr;
    private decimal costoDirecto;
    private decimal imprevistos;
    private decimal precioUnit;
    private decimal ventaUnit;
    private decimal totalPartida;

    // Alertas
    private string alertText = "";
    private bool hasAlerts;
}
