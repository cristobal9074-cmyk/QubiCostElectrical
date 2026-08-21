using System.Collections.ObjectModel;
using System.Windows.Input;
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

    private bool isSectorMatrixMode = true;

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

    public ICommand ShowSectorMatrixCommand { get; private set; } = null!;
    public ICommand ShowResourceDetailCommand { get; private set; } = null!;
    public ICommand ManageZonasCommand { get; private set; } = null!;
    public ICommand OpenHojaCubicacionCommand { get; private set; } = null!;

    private void InitializeSectorMatrix()
    {
        ShowSectorMatrixCommand = new RelayCommand(() => IsSectorMatrixMode = true);
        ShowResourceDetailCommand = new RelayCommand(() => IsSectorMatrixMode = false);
        ManageZonasCommand = new RelayCommand(() => sectorService.ManageZonas(), () => !session.IsReadOnly);
        OpenHojaCubicacionCommand = new RelayCommand(() => sectorService.OpenHojaCubicacion());

        RefreshSectorWindow();
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
