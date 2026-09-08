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

    public SolicitudesViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Permissions) { }

    public SolicitudesViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IPermissionService permissions) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.permissions = permissions;

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

        Reload();
    }

    public void Refresh() => Reload();

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Reload);
    }

    private void Reload()
    {
        Materiales.Clear();
        Epps.Clear();

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

            foreach (var s in session.Solicitudes)
            {
                if (s.Tipo == "Material")
                    Materiales.Add(new SolicitudItemRowVm(this, s));
                else
                    Epps.Add(new SolicitudItemRowVm(this, s));
            }
        }
    }

    private void SeedDefaultSolicitudes()
    {
        var defaults = new List<SolicitudItemRow>
        {
            new() { Tipo = "Material", Codigo = "LH-FLEX-20", Descripcion = "TUBERIA FLEXIBLE LIBRE HALOGENO 20 MM", Unidad = "MTS", Cantidad = 2000 },
            new() { Tipo = "Material", Codigo = "LH-TERM-20", Descripcion = "TERMINAL FLEXIBLE LIBRE HALOGENOS 20 MM arandelas", Unidad = "UN", Cantidad = 1000 },
            new() { Tipo = "Material", Codigo = "LH-COPL-20", Descripcion = "COPLAS FLEXIBLE LIBRE HALOGENO 20 MM", Unidad = "UN", Cantidad = 500 },
            new() { Tipo = "Material", Codigo = "LH-CAJA-58", Descripcion = "CAJAS 5/8 LIBRE HALOGENOS", Unidad = "UN", Cantidad = 1000 },
            new() { Tipo = "Material", Codigo = "", Descripcion = "ESCALERA DE TIJERA 1.74 MTS ALUMINIO", Unidad = "UN", Cantidad = 7, Link = "https://www.sodimac.cl/sodimac-cl/product/101235/escalera-tijera-aluminio" },
            new() { Tipo = "Material", Codigo = "", Descripcion = "TALADRO/ATORNILLADOR GSR 1000 SMART-12V BOSCH", Unidad = "UN", Cantidad = 5, Link = "https://www.easy.cl/taladro-gsr-1000" },
            new() { Tipo = "EPP", Codigo = "EPP-CASCO-B", Descripcion = "Casco de Seguridad Blanco", Unidad = "UN", Cantidad = 10, Observaciones = "Con barboquejo para trabajo en altura" },
            new() { Tipo = "EPP", Codigo = "EPP-GUANTE-C", Descripcion = "Guantes de Cabritilla", Unidad = "PAR", Cantidad = 20, Observaciones = "Para electricista en terreno" }
        };

        session.Solicitudes.AddRange(defaults);
        session.MarkDirty(ProjectDirtyScope.Solicitudes);
    }

    private void AddMaterial()
    {
        if (session.IsReadOnly) return;
        var item = new SolicitudItemRow { Tipo = "Material", Unidad = "UN", Cantidad = 1 };
        Materiales.Add(new SolicitudItemRowVm(this, item));
        SaveToSession();
    }

    private void AddEpp()
    {
        if (session.IsReadOnly) return;
        var item = new SolicitudItemRow { Tipo = "EPP", Unidad = "UN", Cantidad = 1 };
        Epps.Add(new SolicitudItemRowVm(this, item));
        SaveToSession();
    }

    private void DeleteMaterialRow(SolicitudItemRowVm? row)
    {
        if (row == null || session.IsReadOnly) return;
        Materiales.Remove(row);
        SaveToSession();
    }

    private void DeleteEppRow(SolicitudItemRowVm? row)
    {
        if (row == null || session.IsReadOnly) return;
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
                Cantidad = 5
            };
            Epps.Add(new SolicitudItemRowVm(this, item));
        }
        SaveToSession();
    }

    public void SaveToSession()
    {
        if (session.IsReadOnly) return;

        session.Solicitudes.Clear();
        session.Solicitudes.AddRange(Materiales.Select(m => m.ToRow()));
        session.Solicitudes.AddRange(Epps.Select(e => e.ToRow()));
        session.MarkDirty(ProjectDirtyScope.Solicitudes);
    }

    private void SaveAndNotify()
    {
        SaveToSession();
        WpfDialogService.Info("Solicitudes", "Solicitud de materiales y EPP guardada con éxito en el proyecto.");
    }

    // Autocomplete searching logic
    public List<AutocompleteItem> SearchMaterials(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<AutocompleteItem>();

        return session.Materiales
            .Where(m => (m.Codigo ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (m.Descripcion ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(m => new AutocompleteItem
            {
                Codigo = m.Codigo,
                Descripcion = m.Descripcion,
                Unidad = m.Unidad
            })
            .ToList();
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
            WpfDialogService.Error("Error de Vista Previa", $"No se pudo generar la previsualización PDF: {ex.Message}");
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
            WpfDialogService.Error("Error de Vista Previa", $"No se pudo generar la previsualización de Excel: {ex.Message}");
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
                WpfDialogService.Error("Error", "No se pudo exportar el paquete: " + ex.Message);
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
                                Observaciones = wsMat.Cell(r, 6).Value.ToString()
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
                                Observaciones = wsEpp.Cell(r, 6).Value.ToString()
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
                        WpfDialogService.Info("Importación Exitosa", $"Se cargaron {tempMat.Count} materiales y {tempEpp.Count} EPPs desde el archivo Excel.");
                    }
                    else
                    {
                        throw new Exception("No se encontraron filas válidas para importar en el archivo Excel.");
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
            session.ProjectInfo.SolicitudCorrelativo++;
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            OnPropertyChanged(nameof(CodigoSolicitud));
            StatusLabel = "Enviado";

            WpfDialogService.Info("Solicitud Aprobada", $"La solicitud de materiales ha sido aprobada oficialmente. Próximo correlativo: {CodigoSolicitud}.");
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error", "No se pudo aprobar la solicitud: " + ex.Message);
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
