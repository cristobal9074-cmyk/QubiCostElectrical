using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public sealed partial class SolicitudesViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private static readonly List<AutocompleteItem> EppCatalog = new()
    {
        new AutocompleteItem { Codigo = "EPP-CASCO-B", Descripcion = "Casco de Seguridad Blanco", Unidad = "UN" },
        new AutocompleteItem { Codigo = "EPP-GUANTE-C", Descripcion = "Guantes de Cabritilla", Unidad = "PAR" },
        new AutocompleteItem { Codigo = "EPP-ZAPAT-S", Descripcion = "Zapatos de Seguridad Diag.", Unidad = "PAR" },
        new AutocompleteItem { Codigo = "EPP-LENTE-C", Descripcion = "Lentes de Seguridad Claros", Unidad = "UN" },
        new AutocompleteItem { Codigo = "EPP-CHAL-G", Descripcion = "Chaleco Reflectante Geólogo", Unidad = "UN" },
        new AutocompleteItem { Codigo = "EPP-ARNES-4", Descripcion = "Arnés de Seguridad 4 Argollas", Unidad = "UN" },
        new AutocompleteItem { Codigo = "EPP-TAPA-O", Descripcion = "Tapones Auditivos de Silicona", Unidad = "PAR" },
        new AutocompleteItem { Codigo = "EPP-MASC-95", Descripcion = "Mascarilla N95 descartable", Unidad = "CJA" }
    };

    public ICommand AddMaterialCommand { get; }
    public ICommand AddEppCommand { get; }
    public ICommand DeleteMaterialCommand { get; }
    public ICommand DeleteEppCommand { get; }
    public ICommand AddEppPresetCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ImportPacketCommand { get; }
    public ICommand ExportPacketCommand { get; }
    public ICommand ApproveSolicitudCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand SendSolicitudCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenMaterialPickerCommand { get; }
    public ICommand ReutilizarSolicitudCommand { get; }
    public ICommand ExportarHistorialPdfCommand { get; }
    public ICommand ExportarHistorialExcelCommand { get; }

    public SolicitudesViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Permissions, host.MaterialControl, host.UndoService) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public SolicitudesViewModel(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService workspace,
        IPermissionService permissions,
        IMaterialControlService materialControl,
        WpfUndoService? undo = null) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.permissions = permissions;
        this.materialControl = materialControl;
        this.undo = undo;

        AddMaterialCommand = new RelayCommand(AddMaterial);
        AddEppCommand = new RelayCommand(AddEpp);
        DeleteMaterialCommand = new RelayCommand<SolicitudItemRowVm>(DeleteMaterialRow);
        DeleteEppCommand = new RelayCommand<SolicitudItemRowVm>(DeleteEppRow);
        AddEppPresetCommand = new RelayCommand<string>(AddEppPreset);
        SaveCommand = new RelayCommand(SaveAndNotify);
        ExportPdfCommand = new RelayCommand(ExportPdf);
        ExportExcelCommand = new RelayCommand(ExportExcel);
        ImportPacketCommand = new RelayCommand(ImportPacket, () => HasProject && !session.IsReadOnly && CanImportSolicitud);
        ExportPacketCommand = new RelayCommand(ExportPacket, () => HasProject);
        ApproveSolicitudCommand = new RelayCommand(ApproveSolicitud, () => HasProject && !session.IsReadOnly && CanApproveSolicitud);
        OpenLinkCommand = new RelayCommand<string>(OpenLink);
        SendSolicitudCommand = new RelayCommand(SendSolicitud, () => HasProject && !session.IsReadOnly);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());

        OpenMaterialPickerCommand = new RelayCommand(OpenMaterialPicker, () => HasProject && !session.IsReadOnly);
        ReutilizarSolicitudCommand = new RelayCommand<HistorialSolicitudVm>(ReutilizarSolicitud, h => h != null && !session.IsReadOnly);
        ExportarHistorialPdfCommand = new RelayCommand<HistorialSolicitudVm>(ExportarHistorialPdf, h => h != null);
        ExportarHistorialExcelCommand = new RelayCommand<HistorialSolicitudVm>(ExportarHistorialExcel, h => h != null);

        Reload();
    }

    public void Refresh() => Reload();

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(Reload);
        }
        else if (e.Kind == ProjectSessionChangeKind.ProjectInfo && workspace.HasOpenProject)
        {
            RequestedBy = session.ProjectInfo.UsuarioNombre;
            WorkName = session.ProjectInfo.Nombre;
        }
    }

    private void Reload()
    {
        InvalidateMaterialCache();
        Materiales.Clear();
        Epps.Clear();
        HistorialSolicitudes.Clear();

        bool hasProject = workspace.HasOpenProject;
        ShowEmptyState = !hasProject;

        if (hasProject)
        {
            RequestedBy = session.ProjectInfo.UsuarioNombre;
            WorkName = session.ProjectInfo.Nombre;
            RequestDate = AppClock.Now.ToString("dd/MM/yyyy");

            // Seed default values if empty
            if (session.Solicitudes.Count == 0)
            {
                SeedDefaultSolicitudes();
            }

            // Filtrar ítems de la solicitud actual (borrador en curso)
            var currentRows = session.Solicitudes
                .Where(s => string.Equals(s.SolicitudCodigo, CodigoSolicitud, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Si no hay filas con el correlativo actual pero existen filas en borrador/sin aprobar, cargarlas
            if (currentRows.Count == 0 && session.Solicitudes.Any(s => s.Estado != "Aprobado"))
            {
                currentRows = session.Solicitudes.Where(s => s.Estado != "Aprobado").ToList();
            }

            foreach (var s in currentRows)
            {
                if (s.Tipo == "Material")
                    Materiales.Add(new SolicitudItemRowVm(this, s));
                else
                    Epps.Add(new SolicitudItemRowVm(this, s));
            }

            ReloadHistorial();
        }
    }

    private void ReloadHistorial()
    {
        HistorialSolicitudes.Clear();
        if (session == null || session.Solicitudes.Count == 0) return;

        var groups = session.Solicitudes
            .GroupBy(s => string.IsNullOrWhiteSpace(s.SolicitudCodigo) ? "SM 1" : s.SolicitudCodigo.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Key)
            .ToList();

        foreach (var group in groups)
        {
            var first = group.First();
            var itemsList = group.Select(row => new SolicitudItemRowVm(this, row)).ToList();

            var histVm = new HistorialSolicitudVm
            {
                Codigo = group.Key,
                Fecha = first.Fecha,
                SolicitadoPor = first.SolicitadoPor,
                Estado = group.All(x => x.Estado == "Aprobado") ? "Aprobado" : first.Estado,
                ItemsCount = itemsList.Count,
                TotalUnidades = itemsList.Sum(x => x.Cantidad)
            };

            foreach (var item in itemsList)
            {
                histVm.Items.Add(item);
            }

            HistorialSolicitudes.Add(histVm);
        }

        if (SelectedHistorialSolicitud == null || !HistorialSolicitudes.Contains(SelectedHistorialSolicitud))
        {
            SelectedHistorialSolicitud = HistorialSolicitudes.FirstOrDefault();
        }
    }

    private void SeedDefaultSolicitudes()
    {
        var defaults = new List<SolicitudItemRow>
        {
            new() { Tipo = "Material", Codigo = "LH-FLEX-20", Descripcion = "TUBERIA FLEXIBLE LIBRE HALOGENO 20 MM", Unidad = "MTS", Cantidad = 2000, SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" },
            new() { Tipo = "Material", Codigo = "LH-TERM-20", Descripcion = "TERMINAL FLEXIBLE LIBRE HALOGENOS 20 MM arandelas", Unidad = "UN", Cantidad = 1000, SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" },
            new() { Tipo = "Material", Codigo = "LH-COPL-20", Descripcion = "COPLAS FLEXIBLE LIBRE HALOGENO 20 MM", Unidad = "UN", Cantidad = 500, SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" },
            new() { Tipo = "Material", Codigo = "LH-CAJA-58", Descripcion = "CAJAS 5/8 LIBRE HALOGENOS", Unidad = "UN", Cantidad = 1000, SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" },
            new() { Tipo = "Material", Codigo = "", Descripcion = "ESCALERA DE TIJERA 1.74 MTS ALUMINIO", Unidad = "UN", Cantidad = 7, Link = "https://www.sodimac.cl/sodimac-cl/product/101235/escalera-tijera-aluminio", SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" },
            new() { Tipo = "Material", Codigo = "", Descripcion = "TALADRO/ATORNILLADOR GSR 1000 SMART-12V BOSCH", Unidad = "UN", Cantidad = 5, Link = "https://www.easy.cl/taladro-gsr-1000", SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" },
            new() { Tipo = "EPP", Codigo = "EPP-CASCO-B", Descripcion = "Casco de Seguridad Blanco", Unidad = "UN", Cantidad = 10, Observaciones = "Con barboquejo para trabajo en altura", SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" },
            new() { Tipo = "EPP", Codigo = "EPP-GUANTE-C", Descripcion = "Guantes de Cabritilla", Unidad = "PAR", Cantidad = 20, Observaciones = "Para electricista en terreno", SolicitudCodigo = "SM 1", Fecha = "12/10/2026", SolicitadoPor = "Juan Pérez (Jefe Obra)", Estado = "Aprobado" }
        };

        session.Solicitudes.AddRange(defaults);
        session.MarkDirty(ProjectDirtyScope.Solicitudes);
    }

    private void AddMaterial()
    {
        if (session.IsReadOnly) return;
        var item = new SolicitudItemRow { Tipo = "Material", Unidad = "UN", Cantidad = 1, SolicitudCodigo = CodigoSolicitud, Fecha = RequestDate, SolicitadoPor = RequestedBy, Estado = "Borrador" };
        Materiales.Add(new SolicitudItemRowVm(this, item));
        SaveToSession();
    }

    private void AddEpp()
    {
        if (session.IsReadOnly) return;
        var item = new SolicitudItemRow { Tipo = "EPP", Unidad = "UN", Cantidad = 1, SolicitudCodigo = CodigoSolicitud, Fecha = RequestDate, SolicitadoPor = RequestedBy, Estado = "Borrador" };
        Epps.Add(new SolicitudItemRowVm(this, item));
        SaveToSession();
    }

    private void DeleteMaterialRow(SolicitudItemRowVm? row)
    {
        if (row == null || session.IsReadOnly) return;
        if (!string.IsNullOrWhiteSpace(row.Codigo) || !string.IsNullOrWhiteSpace(row.Descripcion))
        {
            var desc = !string.IsNullOrWhiteSpace(row.Descripcion) ? row.Descripcion : row.Codigo;
            if (!WpfDialogService.Confirm("Eliminar ítem", $"¿Eliminar '{desc}' de la solicitud?", isWarning: true))
                return;
        }
        undo?.PushUndoPoint(session, "Eliminar ítem de solicitud");
        Materiales.Remove(row);
        SaveToSession();
    }

    private void DeleteEppRow(SolicitudItemRowVm? row)
    {
        if (row == null || session.IsReadOnly) return;
        if (!string.IsNullOrWhiteSpace(row.Codigo) || !string.IsNullOrWhiteSpace(row.Descripcion))
        {
            var desc = !string.IsNullOrWhiteSpace(row.Descripcion) ? row.Descripcion : row.Codigo;
            if (!WpfDialogService.Confirm("Eliminar EPP", $"¿Eliminar '{desc}' de la solicitud?", isWarning: true))
                return;
        }
        undo?.PushUndoPoint(session, "Eliminar EPP de solicitud");
        Epps.Remove(row);
        SaveToSession();
    }

    private void AddEppPreset(string? presetKey)
    {
        if (string.IsNullOrEmpty(presetKey) || session.IsReadOnly) return;

        var catalogItem = EppCatalog.FirstOrDefault(e => e.Codigo.Equals(presetKey, StringComparison.OrdinalIgnoreCase));
        if (catalogItem == null) return;

        // Check if already in list
        var existing = Epps.FirstOrDefault(e => e.Codigo == catalogItem.Codigo);
        if (existing != null)
        {
            existing.Cantidad += 5;
        }
        else
        {
            var item = new SolicitudItemRow
            {
                Tipo = "EPP",
                Codigo = catalogItem.Codigo,
                Descripcion = catalogItem.Descripcion,
                Unidad = catalogItem.Unidad,
                Cantidad = 5,
                SolicitudCodigo = CodigoSolicitud,
                Fecha = RequestDate,
                SolicitadoPor = RequestedBy,
                Estado = "Borrador"
            };
            Epps.Add(new SolicitudItemRowVm(this, item));
        }
        SaveToSession();
    }

    public void SaveToSession()
    {
        if (session.IsReadOnly) return;

        // Mantener intactas las solicitudes históricas (otro correlativo)
        var otherItems = session.Solicitudes
            .Where(s => !string.Equals(s.SolicitudCodigo, CodigoSolicitud, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var currentItems = new List<SolicitudItemRow>();
        currentItems.AddRange(Materiales.Select(m => m.ToRow()));
        currentItems.AddRange(Epps.Select(e => e.ToRow()));

        session.Solicitudes.Clear();
        session.Solicitudes.AddRange(otherItems);
        session.Solicitudes.AddRange(currentItems);
        session.MarkDirty(ProjectDirtyScope.Solicitudes);

        ReloadHistorial();
    }

    private void SaveAndNotify()
    {
        SaveToSession();
        WpfDialogService.Info("Solicitudes", "Solicitud de materiales y EPP guardada con éxito en el proyecto.");
    }

    // Búsqueda rápida y autocompletado desde Cubicación y Materiales
    public AutocompleteItem? FindMaterialByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var cub = session.Cubicacion
            .FirstOrDefault(c => string.Equals(c.Codigo, code, StringComparison.OrdinalIgnoreCase));
        if (cub != null)
        {
            return new AutocompleteItem
            {
                Codigo = cub.Codigo ?? "",
                Descripcion = cub.Descripcion ?? "",
                Unidad = cub.Unidad ?? "UN"
            };
        }

        var mat = session.Materiales
            .FirstOrDefault(m => string.Equals(m.Codigo, code, StringComparison.OrdinalIgnoreCase));
        if (mat != null)
        {
            return new AutocompleteItem
            {
                Codigo = mat.Codigo ?? "",
                Descripcion = mat.Descripcion ?? "",
                Unidad = mat.Unidad ?? "UN"
            };
        }

        return null;
    }

    public List<AutocompleteItem> SearchMaterials(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<AutocompleteItem>();

        var list = new List<AutocompleteItem>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Desde Cubicación del proyecto (recursos reales computados)
        foreach (var c in session.Cubicacion)
        {
            if (!string.IsNullOrWhiteSpace(c.Codigo) && seenCodes.Add(c.Codigo))
            {
                if (c.Codigo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (c.Descripcion ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new AutocompleteItem
                    {
                        Codigo = c.Codigo,
                        Descripcion = c.Descripcion ?? "",
                        Unidad = c.Unidad ?? "UN"
                    });
                }
            }
        }

        // 2. Desde banco de materiales del proyecto
        foreach (var m in session.Materiales)
        {
            if (!string.IsNullOrWhiteSpace(m.Codigo) && seenCodes.Add(m.Codigo))
            {
                if (m.Codigo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (m.Descripcion ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new AutocompleteItem
                    {
                        Codigo = m.Codigo,
                        Descripcion = m.Descripcion ?? "",
                        Unidad = m.Unidad ?? "UN"
                    });
                }
            }
        }

        return list.Take(15).ToList();
    }

    public List<AutocompleteItem> SearchEpp(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<AutocompleteItem>();

        return EppCatalog
            .Where(e => e.Codigo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        e.Descripcion.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();
    }

    private List<SolicitudAutocompleteSuggestionVm>? _cachedSuggestions;

    public void InvalidateMaterialCache() => _cachedSuggestions = null;

    private List<SolicitudAutocompleteSuggestionVm> GetCachedSuggestions()
    {
        if (_cachedSuggestions != null) return _cachedSuggestions;

        var list = new List<SolicitudAutocompleteSuggestionVm>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var controlRows = materialControl.Build(session.Cubicacion, session.Materiales, session.Solicitudes);
        foreach (var r in controlRows)
        {
            if (string.IsNullOrWhiteSpace(r.Codigo) && string.IsNullOrWhiteSpace(r.Descripcion))
                continue;

            string code = r.Codigo ?? "";
            if (!string.IsNullOrEmpty(code) && !seenCodes.Add(code))
                continue;

            list.Add(new SolicitudAutocompleteSuggestionVm
            {
                Codigo = code,
                Descripcion = r.Descripcion ?? "",
                Unidad = string.IsNullOrWhiteSpace(r.Unidad) ? "UN" : r.Unidad,
                Categoria = r.Categoria ?? "",
                CantidadCubicada = r.CantidadCubicada,
                CantidadSolicitada = r.CantidadSolicitada
            });
        }

        foreach (var m in session.Materiales)
        {
            if (string.IsNullOrWhiteSpace(m.Codigo) && string.IsNullOrWhiteSpace(m.Descripcion))
                continue;

            string code = m.Codigo ?? "";
            if (!string.IsNullOrEmpty(code) && !seenCodes.Add(code))
                continue;

            list.Add(new SolicitudAutocompleteSuggestionVm
            {
                Codigo = code,
                Descripcion = m.Descripcion ?? "",
                Unidad = string.IsNullOrWhiteSpace(m.Unidad) ? "UN" : m.Unidad,
                Categoria = m.Categoria ?? "",
                CantidadCubicada = 0,
                CantidadSolicitada = 0
            });
        }

        _cachedSuggestions = list;
        return list;
    }

    public void SearchMaterialsAutocomplete(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            AutocompleteResults.Clear();
            AutocompleteVisible = false;
            return;
        }

        var q = query.Trim();
        var tokens = q.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var cached = GetCachedSuggestions();
        var scored = new List<(SolicitudAutocompleteSuggestionVm Item, int Score)>();

        for (int i = 0; i < cached.Count; i++)
        {
            var item = cached[i];
            int score = 0;

            if (!string.IsNullOrEmpty(item.Codigo))
            {
                if (string.Equals(item.Codigo, q, StringComparison.OrdinalIgnoreCase))
                    score += 1000;
                else if (item.Codigo.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                    score += 500;
                else if (item.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase))
                    score += 250;
            }

            if (!string.IsNullOrEmpty(item.Descripcion))
            {
                if (item.Descripcion.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                    score += 300;
                else if (tokens.Length > 0 && tokens.All(t => item.Descripcion.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    score += 150;
                else if (tokens.Length > 0 && tokens.Any(t => item.Descripcion.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    score += 50;
            }

            if (score > 0)
            {
                scored.Add((item, score));
            }
        }

        var sorted = scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.CantidadDisponible > 0)
            .Select(x => x.Item)
            .Take(25)
            .ToList();

        AutocompleteResults.Clear();
        for (int i = 0; i < sorted.Count; i++)
        {
            AutocompleteResults.Add(sorted[i]);
        }

        AutocompleteVisible = AutocompleteResults.Count > 0;
    }

    private void OpenMaterialPicker()
    {
        if (!HasProject || session.IsReadOnly) return;

        var items = BuildPickerItems();
        if (items.Count == 0)
        {
            WpfDialogService.Info("Sin Materiales Cubicados", "El proyecto no contiene recursos cubicados en las partidas o banco de materiales.");
            return;
        }

        var dlg = new SolicitudMaterialPickerDialog(items);
        if (System.Windows.Application.Current?.MainWindow != null)
        {
            dlg.Owner = System.Windows.Application.Current.MainWindow;
        }

        if (dlg.ShowDialog() == true && dlg.SelectedItems.Count > 0)
        {
            foreach (var sel in dlg.SelectedItems)
            {
                var existing = Materiales.FirstOrDefault(m => string.Equals(m.Codigo, sel.Codigo, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Cantidad += (sel.CantidadDisponible > 0 ? sel.CantidadDisponible : 1);
                }
                else
                {
                    var newRow = new SolicitudItemRow
                    {
                        Tipo = "Material",
                        Codigo = sel.Codigo,
                        Descripcion = sel.Descripcion,
                        Unidad = sel.Unidad,
                        Cantidad = sel.CantidadDisponible > 0 ? sel.CantidadDisponible : 1,
                        SolicitudCodigo = CodigoSolicitud,
                        Fecha = RequestDate,
                        SolicitadoPor = RequestedBy,
                        Estado = "Borrador"
                    };
                    Materiales.Add(new SolicitudItemRowVm(this, newRow));
                }
            }
            SaveToSession();
            WpfDialogService.Info("Materiales Agregados", $"Se han agregado {dlg.SelectedItems.Count} material(es) a la solicitud desde la cubicación.");
        }
    }

    private List<MaterialPickerItem> BuildPickerItems()
    {
        var controlRows = materialControl.Build(session.Cubicacion, session.Materiales, session.Solicitudes);
        return controlRows
            .OrderBy(r => r.Codigo)
            .Select(r => new MaterialPickerItem
            {
                Codigo = r.Codigo,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Categoria = r.Categoria,
                CantidadCubicada = r.CantidadCubicada,
                CantidadSolicitada = r.CantidadSolicitada
            })
            .ToList();
    }

    private void ExportPdf()
    {
        SaveToSession();
        try
        {
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QubiCost");
            System.IO.Directory.CreateDirectory(tempFolder);
            var cleanName = string.IsNullOrWhiteSpace(WorkName) ? "Proyecto" : WorkName.Replace(" ", "_");
            var path = System.IO.Path.Combine(tempFolder, $"Solicitud_Materiales_{cleanName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            SolicitudesPdfExporter.Export(path, CodigoSolicitud, WorkName, RequestedBy, RequestDate, Materiales.ToList(), Epps.ToList());

            var dialog = new SolicitudPdfPreviewDialog(path);
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                dialog.Owner = System.Windows.Application.Current.MainWindow;
            }
            if (dialog.ShowDialog() == true)
            {
                session.ProjectInfo.SolicitudCorrelativo++;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                OnPropertyChanged(nameof(CodigoSolicitud));
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error de Vista Previa", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void ExportExcel()
    {
        SaveToSession();
        try
        {
            var dialog = new SolicitudExcelPreviewDialog(CodigoSolicitud, WorkName, RequestedBy, Materiales.ToList(), Epps.ToList());
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                dialog.Owner = System.Windows.Application.Current.MainWindow;
            }
            if (dialog.ShowDialog() == true)
            {
                session.ProjectInfo.SolicitudCorrelativo++;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                OnPropertyChanged(nameof(CodigoSolicitud));
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error de Vista Previa", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void ExportPacket()
    {
        SaveToSession();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar Paquete de Solicitud",
            Filter = "Paquete de Solicitud (*.qcreq)|*.qcreq",
            FileName = $"Pedido_{WorkName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.qcreq"
        };

        if (dialog.ShowDialog() == true)
        {
            if (OperationFailureMessages.IsFileLocked(dialog.FileName))
            {
                WpfDialogService.Error("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                return;
            }

            try
            {
                var packet = new SolicitudPacket
                {
                    Correlativo = CodigoSolicitud,
                    Obra = WorkName,
                    SolicitadoPor = RequestedBy,
                    Fecha = RequestDate,
                    Estado = EstadoSolicitud.Pendiente,
                    Items = session.Solicitudes.ToList()
                };

                packet.SignatureHash = ComputeSignature(packet);

                var json = JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                WpfDialogService.Info("Exportar Paquete", "Paquete de solicitud (.qcreq) exportado con éxito para envío.");
            }
            catch (Exception ex)
            {
                WpfDialogService.Error("Error al exportar", OperationFailureMessages.ToUserMessage(ex));
            }
        }
    }

    private void ImportPacket()
    {
        if (session.IsReadOnly) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importar Solicitud de Pedido",
            Filter = "Archivos de Solicitud (*.qcreq;*.xlsx)|*.qcreq;*.xlsx|Paquete de Solicitud (*.qcreq)|*.qcreq|Libro de Excel (*.xlsx)|*.xlsx"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (ext == ".qcreq")
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var packet = JsonSerializer.Deserialize<SolicitudPacket>(json);
                    if (packet == null) throw new Exception("El paquete está vacío o corrupto.");

                    var expectedHash = ComputeSignature(packet);
                    if (packet.SignatureHash != expectedHash)
                    {
                        bool proceed = WpfDialogService.Confirm(
                            "Firma de Integridad Inválida",
                            "ADVERTENCIA DE SEGURIDAD: El hash de integridad de este archivo de solicitud no coincide. " +
                            "Esto indica que el archivo pudo haber sido modificado o editado manualmente fuera de Qubi Cost. " +
                            "\n\n¿Desea importar la solicitud de todas formas?",
                            isWarning: true);

                        if (!proceed)
                            return;
                    }

                    WorkName = packet.Obra;
                    RequestedBy = packet.SolicitadoPor;
                    RequestDate = packet.Fecha;

                    Materiales.Clear();
                    Epps.Clear();
                    foreach (var item in packet.Items)
                    {
                        if (string.IsNullOrWhiteSpace(item.SolicitudCodigo)) item.SolicitudCodigo = packet.Correlativo;
                        if (string.IsNullOrWhiteSpace(item.Fecha)) item.Fecha = packet.Fecha;
                        if (string.IsNullOrWhiteSpace(item.SolicitadoPor)) item.SolicitadoPor = packet.SolicitadoPor;

                        if (item.Tipo == "Material")
                            Materiales.Add(new SolicitudItemRowVm(this, item));
                        else
                            Epps.Add(new SolicitudItemRowVm(this, item));
                    }
                    SaveToSession();
                    WpfDialogService.Info("Importación Exitosa", $"Se cargó la solicitud del proyecto '{packet.Obra}' enviada por '{packet.SolicitadoPor}'.");
                }
                else if (ext == ".xlsx")
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook(dialog.FileName);
                    var wsMat = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Materiales"));
                    var tempMat = new List<SolicitudItemRow>();
                    if (wsMat != null)
                    {
                        var cellObra = wsMat.Cell(2, 2).Value.ToString();
                        var cellSolicitado = wsMat.Cell(3, 2).Value.ToString();

                        if (!string.IsNullOrWhiteSpace(cellObra)) WorkName = cellObra;
                        if (!string.IsNullOrWhiteSpace(cellSolicitado)) RequestedBy = cellSolicitado;

                        var lastRowUsed = wsMat.LastRowUsed();
                        int lastRow = lastRowUsed != null ? lastRowUsed.RowNumber() : 5;
                        for (int r = 6; r <= lastRow; r++)
                        {
                            var desc = wsMat.Cell(r, 2).Value.ToString();
                            if (string.IsNullOrWhiteSpace(desc)) continue;

                            tempMat.Add(new SolicitudItemRow
                            {
                                Tipo = "Material",
                                Codigo = wsMat.Cell(r, 1).Value.ToString(),
                                Descripcion = desc,
                                Unidad = wsMat.Cell(r, 3).Value.ToString(),
                                Cantidad = decimal.TryParse(wsMat.Cell(r, 4).Value.ToString(), out var cant) ? cant : 1,
                                Link = wsMat.Cell(r, 5).Value.ToString(),
                                Observaciones = wsMat.Cell(r, 6).Value.ToString(),
                                SolicitudCodigo = CodigoSolicitud,
                                Fecha = RequestDate,
                                SolicitadoPor = RequestedBy
                            });
                        }
                    }

                    var wsEpp = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("EPP"));
                    var tempEpp = new List<SolicitudItemRow>();
                    if (wsEpp != null)
                    {
                        var lastRowUsed = wsEpp.LastRowUsed();
                        int lastRow = lastRowUsed != null ? lastRowUsed.RowNumber() : 5;
                        for (int r = 6; r <= lastRow; r++)
                        {
                            var desc = wsEpp.Cell(r, 2).Value.ToString();
                            if (string.IsNullOrWhiteSpace(desc)) continue;

                            tempEpp.Add(new SolicitudItemRow
                            {
                                Tipo = "EPP",
                                Codigo = wsEpp.Cell(r, 1).Value.ToString(),
                                Descripcion = desc,
                                Unidad = wsEpp.Cell(r, 3).Value.ToString(),
                                Cantidad = decimal.TryParse(wsEpp.Cell(r, 4).Value.ToString(), out var cant) ? cant : 1,
                                Link = wsEpp.Cell(r, 5).Value.ToString(),
                                Observaciones = wsEpp.Cell(r, 6).Value.ToString(),
                                SolicitudCodigo = CodigoSolicitud,
                                Fecha = RequestDate,
                                SolicitadoPor = RequestedBy
                            });
                        }
                    }

                    if (tempMat.Count > 0 || tempEpp.Count > 0)
                    {
                        Materiales.Clear();
                        Epps.Clear();
                        foreach (var m in tempMat) Materiales.Add(new SolicitudItemRowVm(this, m));
                        foreach (var e in tempEpp) Epps.Add(new SolicitudItemRowVm(this, e));
                        SaveToSession();
                        WpfDialogService.Info("Importación Exitosa", $"Se cargaron {tempMat.Count} materiales y {tempEpp.Count} EPPs desde el archivo.");
                    }
                    else
                    {
                        throw new Exception("No se encontraron filas válidas para importar en el archivo.");
                    }
                }
            }
            catch (Exception ex)
            {
                WpfDialogService.Error("Error de Importación", "No se pudo importar el archivo: " + ex.Message);
            }
        }
    }

    private void SendSolicitud()
    {
        if (!HasProject || session.IsReadOnly)
            return;

        StatusLabel = "Enviado";
        WpfDialogService.Info("Solicitudes", "La solicitud quedó marcada como Enviado (estado de sesión).");
    }

    private void ApproveSolicitud()
    {
        if (session.IsReadOnly) return;

        try
        {
            var currentCodigo = CodigoSolicitud;

            // 1. Marcar ítems actuales como Aprobados con el código vigente
            foreach (var m in Materiales)
            {
                m.SolicitudCodigo = currentCodigo;
                m.Fecha = RequestDate;
                m.SolicitadoPor = RequestedBy;
                m.Estado = "Aprobado";
            }
            foreach (var e in Epps)
            {
                e.SolicitudCodigo = currentCodigo;
                e.Fecha = RequestDate;
                e.SolicitadoPor = RequestedBy;
                e.Estado = "Aprobado";
            }

            SaveToSession();

            // 2. Incrementar correlativo para iniciar la siguiente solicitud
            session.ProjectInfo.SolicitudCorrelativo++;
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            OnPropertyChanged(nameof(CodigoSolicitud));

            // 3. Limpiar borrador actual para la nueva solicitud
            Materiales.Clear();
            Epps.Clear();
            SaveToSession();

            StatusLabel = "Borrador";
            WpfDialogService.Info("Solicitud Aprobada", $"La solicitud '{currentCodigo}' ha sido aprobada oficialmente y archivada en el Historial de Solicitudes.\n\nSe ha iniciado el nuevo borrador: {CodigoSolicitud}.");
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error", "No se pudo aprobar la solicitud: " + ex.Message);
        }
    }

    private void ReutilizarSolicitud(HistorialSolicitudVm? hist)
    {
        if (hist == null || session.IsReadOnly) return;

        bool confirm = WpfDialogService.Confirm(
            "Reutilizar Solicitud",
            $"¿Desea cargar los {hist.ItemsCount} ítems de la solicitud '{hist.Codigo}' en el borrador actual ({CodigoSolicitud})?\n\nLos ítems se agregarán al listado de materiales y EPP listos para ajustar.",
            isWarning: false);

        if (!confirm) return;

        foreach (var item in hist.Items)
        {
            var newRow = new SolicitudItemRow
            {
                Tipo = item.Tipo,
                Codigo = item.Codigo,
                Descripcion = item.Descripcion,
                Unidad = item.Unidad,
                Cantidad = item.Cantidad,
                Link = item.Link,
                Observaciones = item.Observaciones,
                SolicitudCodigo = CodigoSolicitud,
                Fecha = RequestDate,
                SolicitadoPor = RequestedBy,
                Estado = "Borrador"
            };

            if (item.Tipo == "Material")
                Materiales.Add(new SolicitudItemRowVm(this, newRow));
            else
                Epps.Add(new SolicitudItemRowVm(this, newRow));
        }

        SaveToSession();
        SelectedTabIndex = 0; // Cambiar a pestaña Materiales
        WpfDialogService.Info("Ítems Cargados", $"Se cargaron los materiales de '{hist.Codigo}' en el borrador actual.");
    }

    private void ExportarHistorialPdf(HistorialSolicitudVm? hist)
    {
        if (hist == null) return;
        try
        {
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QubiCost");
            System.IO.Directory.CreateDirectory(tempFolder);
            var cleanName = string.IsNullOrWhiteSpace(WorkName) ? "Proyecto" : WorkName.Replace(" ", "_");
            var path = System.IO.Path.Combine(tempFolder, $"Solicitud_{hist.Codigo.Replace(" ", "_")}_{cleanName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            var mats = hist.Items.Where(x => x.Tipo == "Material").ToList();
            var epps = hist.Items.Where(x => x.Tipo != "Material").ToList();

            SolicitudesPdfExporter.Export(path, hist.Codigo, WorkName, hist.SolicitadoPor, hist.Fecha, mats, epps);

            var dialog = new SolicitudPdfPreviewDialog(path);
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                dialog.Owner = System.Windows.Application.Current.MainWindow;
            }
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error de Vista Previa", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void ExportarHistorialExcel(HistorialSolicitudVm? hist)
    {
        if (hist == null) return;
        try
        {
            var mats = hist.Items.Where(x => x.Tipo == "Material").ToList();
            var epps = hist.Items.Where(x => x.Tipo != "Material").ToList();

            var dialog = new SolicitudExcelPreviewDialog(hist.Codigo, WorkName, hist.SolicitadoPor, mats, epps);
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                dialog.Owner = System.Windows.Application.Current.MainWindow;
            }
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error de Vista Previa", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private string ComputeSignature(SolicitudPacket packet)
    {
        var tempItems = new List<string>();
        if (packet.Items != null)
        {
            foreach (var item in packet.Items)
            {
                tempItems.Add($"{item.Codigo}:{item.Cantidad}:{item.Tipo}");
            }
        }
        var serializedItems = string.Join(",", tempItems);
        var projectId = session?.ProjectInfo?.Nombre ?? "QCE_PROJECT";
        var user = session?.ProjectInfo?.UsuarioNombre ?? "USER";
        var rawData = $"{packet.Correlativo}|{packet.Obra}|{packet.SolicitadoPor}|{packet.Fecha}|{serializedItems}|{projectId}|{user}";

        var keyBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"QCE_SIGN_{projectId}_{ProductVersionInfo.DatabaseSchemaVersion}"));
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawData);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!QubiCostElectrical.Wpf.Infrastructure.Security.SafeProcessLauncher.TryOpenUrl(url, out var error))
        {
            WpfDialogService.Error("Error al abrir enlace", error ?? "No se pudo abrir el enlace.");
        }
    }
}
