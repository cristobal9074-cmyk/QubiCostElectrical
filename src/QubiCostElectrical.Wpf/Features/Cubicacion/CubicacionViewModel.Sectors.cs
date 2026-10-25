using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    public const int MaximumSectorCount = 400;
    public const int SectorWindowSize = MaximumSectorCount;

    private bool isSectorMatrixMode = true;
    private bool isZonaLibreMode = false;

    public ObservableCollection<string> VisibleSectorHeaders { get; } = new();

    public bool IsSectorMatrixMode
    {
        get => isSectorMatrixMode;
        set
        {
            if (isSectorMatrixMode == value)
                return;
            if (!TryCommitOpenCell())
                return;

            isSectorMatrixMode = value;
            RefreshSectorWindow();

            OnPropertyChanged(nameof(IsSectorMatrixMode));
            OnPropertyChanged(nameof(IsDetailMode));
            OnPropertyChanged(nameof(IsDetailHeaderVisible));
            OnPropertyChanged(nameof(CanAddExtraCasillas));
            OnPropertyChanged(nameof(SectorModeDescription));
        }
    }

    public bool IsDetailMode => !isSectorMatrixMode;
    public bool IsDetailHeaderVisible => IsDetailMode && HasPartida;

    public bool IsZonaLibreMode
    {
        get => isZonaLibreMode;
        set
        {
            if (isZonaLibreMode == value)
                return;
            SetCubicacionMode(value);
        }
    }

    public bool IsPorZonasMode => !isZonaLibreMode;
    public bool CanAddExtraCasillas => isSectorMatrixMode && isZonaLibreMode;

    public string SectorModeDescription => !isSectorMatrixMode
        ? "Costos, precios y atributos del recurso"
        : (isZonaLibreMode
            ? "Modo Zona Libre · Ingrese cantidades directamente sin desglose de pisos ni sectores"
            : "400 casillas de pisos y sectores · Desplácese con flechas o scroll horizontal");

    public ICommand ShowSectorMatrixCommand { get; private set; } = null!;
    public ICommand ShowResourceDetailCommand { get; private set; } = null!;
    public ICommand SetZonaLibreModeCommand { get; private set; } = null!;
    public ICommand SetPorZonasModeCommand { get; private set; } = null!;
    public ICommand ManageZonasCommand { get; private set; } = null!;
    public ICommand OpenHojaCubicacionCommand { get; private set; } = null!;

    private void InitializeSectorMatrix()
    {
        ShowSectorMatrixCommand = new RelayCommand(() => IsSectorMatrixMode = true);
        ShowResourceDetailCommand = new RelayCommand(() => IsSectorMatrixMode = false);
        SetZonaLibreModeCommand = new RelayCommand(() => SetCubicacionMode(true));
        SetPorZonasModeCommand = new RelayCommand(() => SetCubicacionMode(false));
        ManageZonasCommand = new RelayCommand(() => sectorService.ManageZonas(), () => !session.IsReadOnly);
        OpenHojaCubicacionCommand = new RelayCommand(() => sectorService.OpenHojaCubicacion());

        string defaultPref = preferencesStore?.Load()?.DefaultCubicacionMode ?? "Libre";
        bool preferZonas = string.Equals(defaultPref, "Zonas", StringComparison.OrdinalIgnoreCase);
        bool hasActiveZones = session.ZonasProyecto?.Any(z => z != null && z.Activa) == true;

        isZonaLibreMode = !(preferZonas || hasActiveZones);
        isSectorMatrixMode = true;
        RefreshSectorWindow();
    }

    private void SetCubicacionMode(bool libre)
    {
        if (isZonaLibreMode == libre)
            return;
        if (!TryCommitOpenCell())
            return;

        isZonaLibreMode = libre;
        try
        {
            var prefs = preferencesStore?.Load();
            if (prefs != null)
            {
                prefs.DefaultCubicacionMode = libre ? "Libre" : "Zonas";
                preferencesStore?.Save(prefs);
            }
        }
        catch { }

        RefreshSectorWindow();
        OnPropertyChanged(nameof(IsZonaLibreMode));
        OnPropertyChanged(nameof(IsPorZonasMode));
        OnPropertyChanged(nameof(CanAddExtraCasillas));
        OnPropertyChanged(nameof(SectorModeDescription));
    }

    public void PickSectorValueFromHojaCubicacion(CubicacionRowVm row, int absoluteOffset)
        => sectorService.PickSectorValueFromHojaCubicacion(row, absoluteOffset);

    public void PickQuantityFromHojaCubicacion(CubicacionRowVm row)
        => sectorService.PickQuantityFromHojaCubicacion(row);

    internal void InitializeSectorRow(CubicacionRowVm row) => sectorService.InitializeSectorRow(row);

    internal void RepairSlaveZones() => sectorService.RepairSlaveZones();

    internal CubicacionRowVm CreateRowVm(CubicacionRow row, bool isSessionBacked = true)
        => new(row, isSessionBacked, () => CanEdit);

    internal void RefreshSectorWindow() => sectorService.RefreshSectorWindow();

    internal void NotifyVisibleSectorHeadersChanged() => OnPropertyChanged(nameof(VisibleSectorHeaders));

    public void SetSectorExpressionDirect(CubicacionRowVm row, int offset, string expression)
        => sectorService.SetSectorExpressionDirect(row, offset, expression);

    public void ApplyBatchSectorExpressions(CubicacionRowVm row, HashSet<int> slots)
        => sectorService.ApplyBatchSectorExpressions(row, slots);
}
