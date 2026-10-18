using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.MaterialControl;

public sealed class MaterialControlRowVm : ViewModelBase
{
    public MaterialControlRow Model { get; }

    public string Codigo => Model.Codigo;
    public string Descripcion => Model.Descripcion;
    public string Unidad => Model.Unidad;
    public string Categoria => Model.Categoria;
    public decimal CantidadCubicada => Model.CantidadCubicada;
    public decimal CantidadSolicitada => Model.CantidadSolicitada;
    public decimal CantidadPendiente => Model.CantidadPendiente;
    public decimal CantidadExceso => Model.CantidadExceso;
    public double PorcentajeAvance => Model.PorcentajeAvance;
    public string EstadoControl => Model.EstadoControl;
    public bool TieneSobrepedido => Model.TieneSobrepedido;
    public bool EstaCompleto => Model.EstaCompleto;
    public bool EstaEnProceso => Model.EstaEnProceso;
    public bool SinPedir => Model.SinPedir;
    public IReadOnlyList<MaterialSolicitudUso> Solicitudes => Model.Solicitudes;

    public double ProgressWidthPct => Math.Clamp(PorcentajeAvance, 0.0, 100.0);

    public MaterialControlRowVm(MaterialControlRow model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }
}

public sealed class MaterialControlViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly IPermissionService permissions;
    private readonly IMaterialControlService materialControl;

    private readonly ObservableCollection<MaterialControlRowVm> rows = new();
    private readonly ObservableCollection<MaterialSolicitudUso> selectedSolicitudes = new();
    private readonly ICollectionView rowsView;
    private readonly System.Windows.Threading.DispatcherTimer searchDebounceTimer;

    private string searchText = "";
    private string filterMode = "all"; // all | alert | pending | completed
    private decimal toleranciaPct = 5m;
    private MaterialControlRowVm? selectedRow;
    private bool showEmptyState = true;

    // Resumen / KPIs
    private decimal totalCubicado;
    private decimal totalSolicitado;
    private decimal totalPendiente;
    private decimal totalExceso;
    private int cantidadSobrepedidos;
    private int cantidadPendientes;
    private int cantidadCompletos;
    private double porcentajeGlobal;
    private string summaryCountText = "0 materiales";

    public MaterialControlViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Permissions, host.Services.MaterialControl) { }

    public MaterialControlViewModel(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService workspace,
        IPermissionService permissions,
        IMaterialControlService materialControl) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.permissions = permissions;
        this.materialControl = materialControl;

        RefreshCommand = new RelayCommand(Refresh);
        ExportExcelCommand = new RelayCommand(ExportExcel, () => HasProject && rows.Count > 0);
        SetFilterCommand = new RelayCommand<string>(SetFilter);
        GoToSolicitudesCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Solicitudes));
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());

        rowsView = CollectionViewSource.GetDefaultView(rows);
        rowsView.Filter = FilterItem;

        searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        searchDebounceTimer.Tick += (_, _) =>
        {
            searchDebounceTimer.Stop();
            rowsView.Refresh();
            UpdateCountText();
        };

        Reload();
    }

    public ObservableCollection<MaterialControlRowVm> Rows => rows;
    public ICollectionView FilteredRows => rowsView;
    public ObservableCollection<MaterialSolicitudUso> SelectedSolicitudes => selectedSolicitudes;

    public bool HasProject => workspace.HasOpenProject;
    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                if (System.Windows.Application.Current == null)
                {
                    rowsView.Refresh();
                    UpdateCountText();
                }
                else
                {
                    searchDebounceTimer.Stop();
                    searchDebounceTimer.Start();
                }
            }
        }
    }

    public string FilterMode
    {
        get => filterMode;
        set
        {
            if (SetProperty(ref filterMode, value))
            {
                rowsView.Refresh();
                UpdateCountText();
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterAlert));
                OnPropertyChanged(nameof(IsFilterPending));
                OnPropertyChanged(nameof(IsFilterCompleted));
            }
        }
    }

    public bool IsFilterAll => filterMode == "all";
    public bool IsFilterAlert => filterMode == "alert";
    public bool IsFilterPending => filterMode == "pending";
    public bool IsFilterCompleted => filterMode == "completed";

    public decimal ToleranciaPct
    {
        get => toleranciaPct;
        set
        {
            if (SetProperty(ref toleranciaPct, Math.Clamp(value, 0m, 50m)))
                Reload();
        }
    }

    public MaterialControlRowVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (SetProperty(ref selectedRow, value))
            {
                UpdateSelectedDetails();
            }
        }
    }

    // KPIs
    public decimal TotalCubicado { get => totalCubicado; private set => SetProperty(ref totalCubicado, value); }
    public decimal TotalSolicitado { get => totalSolicitado; private set => SetProperty(ref totalSolicitado, value); }
    public decimal TotalPendiente { get => totalPendiente; private set => SetProperty(ref totalPendiente, value); }
    public decimal TotalExceso { get => totalExceso; private set => SetProperty(ref totalExceso, value); }
    public int CantidadSobrepedidos { get => cantidadSobrepedidos; private set => SetProperty(ref cantidadSobrepedidos, value); }
    public int CantidadPendientes { get => cantidadPendientes; private set => SetProperty(ref cantidadPendientes, value); }
    public int CantidadCompletos { get => cantidadCompletos; private set => SetProperty(ref cantidadCompletos, value); }
    public double PorcentajeGlobal { get => porcentajeGlobal; private set => SetProperty(ref porcentajeGlobal, value); }
    public bool TieneAlertas => CantidadSobrepedidos > 0;
    public string SummaryCountText { get => summaryCountText; private set => SetProperty(ref summaryCountText, value); }

    // Panel Lateral de Detalle
    public bool HasSelection => SelectedRow != null;
    public string SelectedCodigo => SelectedRow?.Codigo ?? "—";
    public string SelectedDescripcion => SelectedRow?.Descripcion ?? "Seleccione un material";
    public string SelectedUnidad => SelectedRow?.Unidad ?? "UN";
    public decimal SelectedCubicado => SelectedRow?.CantidadCubicada ?? 0m;
    public decimal SelectedSolicitado => SelectedRow?.CantidadSolicitada ?? 0m;
    public decimal SelectedSaldo => SelectedRow?.TieneSobrepedido == true ? SelectedRow.CantidadExceso : (SelectedRow?.CantidadPendiente ?? 0m);
    public string SelectedSaldoLabel => SelectedRow?.TieneSobrepedido == true ? "Exceso en Faena" : "Falta por Pedir";
    public bool SelectedEsSobrepedido => SelectedRow?.TieneSobrepedido == true;
    public string SelectedBadgeStatus => SelectedRow == null ? "SIN SELECCIÓN" : SelectedRow.EstadoControl.ToUpperInvariant();
    public string SelectedPctText => SelectedRow == null ? "" : $"{SelectedRow.PorcentajeAvance:N1}% {(SelectedRow.TieneSobrepedido ? "(Excedido)" : "solicitado")}";
    public double SelectedBarWidthPct => SelectedRow == null ? 0.0 : Math.Clamp(SelectedRow.PorcentajeAvance, 0.0, 100.0);
    public string SelectedSolicitudesCountText => $"{selectedSolicitudes.Count} pedidos registrados";

    public ICommand RefreshCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand GoToSolicitudesCommand { get; }
    public ICommand OpenProjectCommand { get; }

    public void Refresh() => Reload();

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Reload);
    }

    private void Reload()
    {
        rows.Clear();
        selectedSolicitudes.Clear();

        if (HasProject)
        {
            var dataRows = materialControl.Build(session.Cubicacion, session.Materiales, session.Solicitudes, ToleranciaPct);
            foreach (var r in dataRows)
                rows.Add(new MaterialControlRowVm(r));

            var summary = materialControl.GetSummary(dataRows);
            TotalCubicado = summary.TotalCubicado;
            TotalSolicitado = summary.TotalSolicitado;
            TotalPendiente = summary.TotalPendiente;
            TotalExceso = summary.TotalExceso;
            CantidadSobrepedidos = summary.CantidadSobrepedidos;
            CantidadPendientes = summary.CantidadPendientes;
            CantidadCompletos = summary.CantidadCompletos;
            PorcentajeGlobal = summary.PorcentajeGlobal;
            OnPropertyChanged(nameof(TieneAlertas));

            ShowEmptyState = rows.Count == 0;
            if (rows.Count > 0)
                SelectedRow = rows[0];
        }
        else
        {
            ShowEmptyState = true;
            TotalCubicado = 0m;
            TotalSolicitado = 0m;
            TotalPendiente = 0m;
            TotalExceso = 0m;
            CantidadSobrepedidos = 0;
            CantidadPendientes = 0;
            CantidadCompletos = 0;
            PorcentajeGlobal = 0.0;
            OnPropertyChanged(nameof(TieneAlertas));
        }

        rowsView.Refresh();
        UpdateCountText();
        UpdateSelectedDetails();
    }

    private void SetFilter(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        FilterMode = mode.ToLowerInvariant();
    }

    private bool FilterItem(object obj)
    {
        if (obj is not MaterialControlRowVm item) return false;

        // Filtro por modo
        if (FilterMode == "alert" && !item.TieneSobrepedido) return false;
        if (FilterMode == "pending" && (!item.EstaEnProceso && !item.SinPedir)) return false;
        if (FilterMode == "completed" && !item.EstaCompleto) return false;

        // Filtro por texto
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string q = SearchText.Trim();
            bool matchCode = (item.Codigo ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);
            bool matchDesc = (item.Descripcion ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);
            bool matchCat = (item.Categoria ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!matchCode && !matchDesc && !matchCat) return false;
        }

        return true;
    }

    private void UpdateCountText()
    {
        int count = rowsView.Cast<object>().Count();
        SummaryCountText = $"Mostrando {count} de {rows.Count} materiales";
    }

    private void UpdateSelectedDetails()
    {
        selectedSolicitudes.Clear();
        if (SelectedRow != null)
        {
            foreach (var uso in SelectedRow.Solicitudes)
                selectedSolicitudes.Add(uso);
        }

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCodigo));
        OnPropertyChanged(nameof(SelectedDescripcion));
        OnPropertyChanged(nameof(SelectedUnidad));
        OnPropertyChanged(nameof(SelectedCubicado));
        OnPropertyChanged(nameof(SelectedSolicitado));
        OnPropertyChanged(nameof(SelectedSaldo));
        OnPropertyChanged(nameof(SelectedSaldoLabel));
        OnPropertyChanged(nameof(SelectedEsSobrepedido));
        OnPropertyChanged(nameof(SelectedBadgeStatus));
        OnPropertyChanged(nameof(SelectedPctText));
        OnPropertyChanged(nameof(SelectedBarWidthPct));
        OnPropertyChanged(nameof(SelectedSolicitudesCountText));
    }

    private void ExportExcel()
    {
        if (!HasProject || rows.Count == 0) return;

        var sfd = new SaveFileDialog
        {
            Title = "Exportar Informe de Control de Materiales",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            FileName = $"Control_Materiales_{session.ProjectInfo.Nombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (sfd.ShowDialog() == true)
        {
            if (OperationFailureMessages.IsFileLocked(sfd.FileName))
            {
                WpfDialogService.Error("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                return;
            }

            try
            {
                using var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("ControlMateriales");

                ws.Cell(1, 1).Value = "INFORME DE CONTROL DE MATERIALES DE OBRA";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;

                ws.Cell(2, 1).Value = $"Proyecto: {session.ProjectInfo.Nombre} · {session.ProjectInfo.Cliente}";
                ws.Cell(3, 1).Value = $"Fecha de emisión: {DateTime.Now:dd/MM/yyyy HH:mm}";

                int r = 5;
                ws.Cell(r, 1).Value = "Código";
                ws.Cell(r, 2).Value = "Descripción";
                ws.Cell(r, 3).Value = "Unidad";
                ws.Cell(r, 4).Value = "Categoría";
                ws.Cell(r, 5).Value = "Cubicado (Presupuesto)";
                ws.Cell(r, 6).Value = "Solicitado (Faena)";
                ws.Cell(r, 7).Value = "Falta por Pedir";
                ws.Cell(r, 8).Value = "Exceso / Sobrepedido";
                ws.Cell(r, 9).Value = "% Avance";
                ws.Cell(r, 10).Value = "Estado";

                var headerRange = ws.Range(r, 1, r, 10);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromTheme(ClosedXML.Excel.XLThemeColor.Accent1);
                headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

                r++;
                foreach (var row in rows)
                {
                    ws.Cell(r, 1).Value = row.Codigo;
                    ws.Cell(r, 2).Value = row.Descripcion;
                    ws.Cell(r, 3).Value = row.Unidad;
                    ws.Cell(r, 4).Value = row.Categoria;
                    ws.Cell(r, 5).Value = row.CantidadCubicada;
                    ws.Cell(r, 6).Value = row.CantidadSolicitada;
                    ws.Cell(r, 7).Value = row.CantidadPendiente;
                    ws.Cell(r, 8).Value = row.CantidadExceso;
                    ws.Cell(r, 9).Value = row.PorcentajeAvance / 100.0;
                    ws.Cell(r, 10).Value = row.EstadoControl;

                    if (row.TieneSobrepedido)
                    {
                        ws.Range(r, 1, r, 10).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#FEE2E2");
                        ws.Cell(r, 10).Style.Font.FontColor = ClosedXML.Excel.XLColor.Red;
                        ws.Cell(r, 10).Style.Font.Bold = true;
                    }

                    r++;
                }

                ws.Range(6, 5, r - 1, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Range(6, 9, r - 1, 9).Style.NumberFormat.Format = "0.0%";

                ws.Columns().AdjustToContents();
                wb.SaveAs(sfd.FileName);

                WpfDialogService.Info("Exportación Exitosa", "El informe de control de materiales se ha exportado correctamente.");
            }
            catch (Exception ex)
            {
                WpfDialogService.Error("Error de Exportación", OperationFailureMessages.ToUserMessage(ex));
            }
        }
    }
}
