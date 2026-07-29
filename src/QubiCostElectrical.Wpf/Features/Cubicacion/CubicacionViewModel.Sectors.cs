using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    public const int MaximumSectorCount = 300;
    // Antes 15: la matriz mostraba una "ventana" de 15 casillas a la vez y saltar más allá
    // recargaba todo (headers, valores de cada fila) — se sentía como cambiar de zona en vez
    // de desplazarse por una sola hoja de cálculo continua. Ahora las 300 columnas existen
    // siempre como columnas reales de la grilla y WPF las virtualiza
    // (EnableColumnVirtualization), así que mover el foco o hacer scroll no dispara ningún
    // recálculo — igual que desplazarse por una hoja de Excel ancha. Ya no hay concepto de
    // "ventana visible": los offsets de sector (0-299) se usan directos en toda la clase.
    public const int SectorWindowSize = MaximumSectorCount;
    public const int MinSectorColumnWidth = 28;
    public const int MaxSectorColumnWidth = 120;
    public const int SectorColumnWidthStep = 4;

    private bool isSectorMatrixMode = true;
    private int sectorColumnWidth = 52;

    public ObservableCollection<string> VisibleSectorHeaders { get; } = new();

    public bool IsSectorMatrixMode
    {
        get => isSectorMatrixMode;
        set
        {
            if (!SetProperty(ref isSectorMatrixMode, value))
                return;

            OnPropertyChanged(nameof(IsDetailMode));
            OnPropertyChanged(nameof(SectorModeDescription));
        }
    }

    public bool IsDetailMode => !IsSectorMatrixMode;

    public string SectorModeDescription => IsSectorMatrixMode
        ? "300 casillas · desplazate con las flechas o el scroll horizontal"
        : "Costos, precios y atributos del recurso";

    public int SectorColumnWidth
    {
        get => sectorColumnWidth;
        set
        {
            value = Math.Clamp(value, MinSectorColumnWidth, MaxSectorColumnWidth);
            if (SetProperty(ref sectorColumnWidth, value))
            {
                OnPropertyChanged(nameof(SectorColumnWidthText));
            }
        }
    }

    public string SectorColumnWidthText
    {
        get => $"{sectorColumnWidth}px";
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (int.TryParse(value.Replace("px", "").Trim(), out var w))
                SectorColumnWidth = w;
        }
    }

    public ICommand ShowSectorMatrixCommand { get; private set; } = null!;
    public ICommand ShowResourceDetailCommand { get; private set; } = null!;
    public ICommand ManageZonasCommand { get; private set; } = null!;
    public ICommand OpenHojaCubicacionCommand { get; private set; } = null!;
    public ICommand IncreaseSectorWidthCommand { get; private set; } = null!;
    public ICommand DecreaseSectorWidthCommand { get; private set; } = null!;
    public ICommand OpenPrecioListaCommand { get; private set; } = null!;
    public ICommand OpenScResumenCommand { get; private set; } = null!;
    public ICommand OpenScTomarCommand { get; private set; } = null!;
    public ICommand OpenItemizadoCommand { get; private set; } = null!;

    private void InitializeSectorMatrix()
    {
        ShowSectorMatrixCommand = new RelayCommand(() => IsSectorMatrixMode = true);
        ShowResourceDetailCommand = new RelayCommand(() => IsSectorMatrixMode = false);
        ManageZonasCommand = new RelayCommand(() => sectorService.ManageZonas(), () => !session.IsReadOnly);
        OpenHojaCubicacionCommand = new RelayCommand(() => sectorService.OpenHojaCubicacion());
        IncreaseSectorWidthCommand = new RelayCommand(() => SectorColumnWidth += SectorColumnWidthStep);
        DecreaseSectorWidthCommand = new RelayCommand(() => SectorColumnWidth -= SectorColumnWidthStep);
        OpenPrecioListaCommand = new RelayCommand(OpenPrecioLista);
        OpenScResumenCommand = new RelayCommand(OpenCubicacionScResumen);
        OpenScTomarCommand = new RelayCommand(OpenCubicacionScTomar);
        OpenItemizadoCommand = new RelayCommand(OpenCubicacionItemizado);

        RefreshSectorWindow();
    }

    private void OpenPrecioLista()
    {
        var codigo = "";
        if (SelectedRow is { Codigo: not null and not "" })
            codigo = SelectedRow.Codigo;
        else if (SelectedPartida is { Codigo: not null and not "" })
            codigo = SelectedPartida.Codigo;

        if (!string.IsNullOrWhiteSpace(codigo) && priceBankStore.GetByCodigo(codigo) == null)
            codigo = "";

        var dialog = new PrecioListaResumenDialog(session, priceBankStore, codigo) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    private void OpenCubicacionScResumen()
    {
        var sc = session.CubicacionSC;
        if (sc == null || sc.Count == 0)
        {
            WpfDialogService.Info("Sin datos SC", "No hay datos de servicios comunes en el proyecto actual.");
            return;
        }
        var dialog = new CubicacionScResumenDialog(sc, session.ZonasProyecto, session.CubicacionScConfig)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private void OpenCubicacionScTomar()
    {
        var sc = session.CubicacionSC;
        var config = session.CubicacionScConfig;
        if (sc == null || sc.Count == 0 || config == null)
        {
            WpfDialogService.Info("Sin datos SC", "No hay datos de servicios comunes en el proyecto actual.");
            return;
        }
        var dialog = new ResumenScTomarDialog(
            sc,
            config,
            session.ZonasProyecto)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            var row = SelectedRow;
            if (row == null) return;
            var taken = dialog.Result;
            row.Codigo = taken.Nombre;
            row.Descripcion = taken.Nombre;
            row.Unidad = taken.Unidad;
            row.Tipo = "MAT";
            // Apply per-zone quantities to sector values
            var activeZones = session.ZonasProyecto
                .Where(z => z.Activa)
                .OrderBy(z => z.Orden)
                .ToList();
            row.BeginBatchUpdate();
            for (int i = 0; i < taken.ZonaQties.Count && i < activeZones.Count; i++)
            {
                var qty = taken.ZonaQties[i];
                var zoneName = activeZones[i].Nombre;
                if (!string.Equals(qty.ZonaNombre, zoneName, StringComparison.OrdinalIgnoreCase))
                    continue;
                var val = decimal.TryParse(qty.Text, out var v) ? v : 0m;
                if (val > 0m)
                    SetSectorExpressionDirect(row, i, val.ToString("0.##"));
            }
            row.EndBatchUpdate();
            row.RefreshDisplayProperties();
            RightPanel.LoadRow(row);
            ValidateRow(row);
            MarkDirty();
            NotifyCubicacionSummaryChanged();
        }
    }

    private void OpenCubicacionItemizado()
    {
        if (Partidas == null || Partidas.Count == 0)
        {
            WpfDialogService.Info("Sin partidas", "No hay partidas en el proyecto actual.");
            return;
        }

        var costsByItemId = new Dictionary<string, PartidaCost>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in session.Cubicacion)
        {
            var key = row.ItemId ?? "";
            if (key.Length == 0) continue;
            var matRow = row.MatClp * row.Cantidad;
            var moRow = row.MoClp * row.Cantidad;
            if (costsByItemId.TryGetValue(key, out var existing))
                costsByItemId[key] = new PartidaCost(existing.Mat + matRow, existing.Mo + moRow, existing.Total + matRow + moRow);
            else
                costsByItemId[key] = new PartidaCost(matRow, moRow, matRow + moRow);
        }

        var items = new List<ItemizadoPartidaVm>();
        foreach (var partida in Partidas)
        {
            bool isSection = !partida.EsPartida;
            var cost = partida.Id != null && costsByItemId.TryGetValue(partida.Id, out var c)
                ? c
                : new PartidaCost(0, 0, 0);

            items.Add(new ItemizadoPartidaVm
            {
                EsSeccion = isSection,
                Id = partida.Id ?? "",
                ItemCode = isSection ? "" : partida.Codigo,
                Descripcion = partida.Descripcion,
                Unidad = partida.Unidad,
                Cantidad = partida.Cantidad,
                MaterialTotal = cost.Mat,
                ManoObraTotal = cost.Mo,
                CostoDirecto = cost.Total,
            });
        }

        var dialog = new ResumenItemizadoDialog(items, SelectedPartida?.Id)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            var targetId = dialog.Result.Id;
            var target = Partidas.FirstOrDefault(p =>
                string.Equals(p.Id, targetId, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                SelectPartida(target);

                if (dialog.ShouldAddResource && AddRowCommand.CanExecute(null))
                    AddRowCommand.Execute(null);

                if (dialog.ShouldDeleteRows)
                {
                    var ids = session.Cubicacion
                        .Where(r => string.Equals(r.ItemId, targetId, StringComparison.OrdinalIgnoreCase))
                        .Select(r => r.Id)
                        .ToList();
                    if (ids.Count > 0)
                        cubicacionPresenter.DeleteRows(ids);
                }
            }
        }
    }

    // Lógica movida a CubicacionSectorService (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que CubicacionView.xaml.cs, CubicacionRightPanelVm y el resto de los
    // partials ya conocen, sin cambiar ninguna firma.
    public void PickSectorValueFromHojaCubicacion(CubicacionRowVm row, int absoluteOffset)
        => sectorService.PickSectorValueFromHojaCubicacion(row, absoluteOffset);

    internal void InitializeSectorRow(CubicacionRowVm row) => sectorService.InitializeSectorRow(row);

    internal void RefreshSectorWindow() => sectorService.RefreshSectorWindow();

    internal void NotifyVisibleSectorHeadersChanged() => OnPropertyChanged(nameof(VisibleSectorHeaders));

    public void SetSectorExpressionDirect(CubicacionRowVm row, int offset, string expression)
        => sectorService.SetSectorExpressionDirect(row, offset, expression);
}
