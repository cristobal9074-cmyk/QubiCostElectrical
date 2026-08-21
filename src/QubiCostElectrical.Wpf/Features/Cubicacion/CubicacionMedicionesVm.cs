using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class CubicacionMedicionesVm : ViewModelBase
{
    private readonly BudgetProjectSession session;
    private readonly CubicacionViewModel? owner;
    private CubicacionRowVm? currentRow;
    private MedicionLineVm? selectedLine;
    private decimal total;

    public CubicacionMedicionesVm(BudgetProjectSession session, CubicacionViewModel? owner = null)
    {
        this.session = session;
        this.owner = owner;
        AgregarCommand = new RelayCommand(Agregar, () => currentRow != null);
        QuitarCommand = new RelayCommand(Quitar, () => selectedLine != null);
    }

    public ObservableCollection<MedicionLineVm> Lines { get; } = new();

    public MedicionLineVm? SelectedLine
    {
        get => selectedLine;
        set
        {
            SetProperty(ref selectedLine, value);
            (QuitarCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public decimal Total
    {
        get => total;
        private set => SetProperty(ref total, value);
    }

    public string TotalDisplay => Total.ToString("N3");

    public bool HasLines => Lines.Count > 0;

    public ICommand AgregarCommand { get; }
    public ICommand QuitarCommand { get; }

    public void LoadForRow(CubicacionRowVm? row)
    {
        currentRow = row;
        Lines.Clear();
        selectedLine = null;

        if (row != null)
        {
            var rowLines = session.Mediciones
                .Where(m => m.CubicacionRowId == row.Source.Id)
                .OrderBy(m => m.Orden)
                .Select(CreateVm);

            foreach (var vm in rowLines)
                Lines.Add(vm);
        }

        RecalcTotal();
        OnPropertyChanged(nameof(HasLines));
        (AgregarCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private MedicionLineVm CreateVm(MeasurementLineRow source)
    {
        var vm = new MedicionLineVm(source);
        vm.ParcialChanged = OnLineEdited;
        return vm;
    }

    private void Agregar()
    {
        if (currentRow == null) return;

        // Sin esto, agregar una medición a una fila vacía (placeholder, aún no confirmada
        // en session.Cubicacion) creaba una línea huérfana referenciando un Id que nunca
        // llegaba a persistirse.
        owner?.EnsureRowPersisted(currentRow);

        var line = new MeasurementLineRow
        {
            CubicacionRowId = currentRow.Source.Id,
            ItemId = currentRow.Source.ItemId,
            Orden = Lines.Count,
            Signo = 1,
        };
        session.Mediciones.Add(line);
        var vm = CreateVm(line);
        Lines.Add(vm);
        SelectedLine = vm;
        RecalcTotal();
        MarkDirtySuppressed();
        OnPropertyChanged(nameof(HasLines));
    }

    private void Quitar()
    {
        if (selectedLine == null) return;
        session.Mediciones.Remove(selectedLine.Source);
        Lines.Remove(selectedLine);
        SelectedLine = Lines.LastOrDefault();
        RecalcTotal();
        MarkDirtySuppressed();
        OnPropertyChanged(nameof(HasLines));
    }

    private void OnLineEdited()
    {
        RecalcTotal();
        MarkDirtySuppressed();
    }

    // Rows ya refleja el cambio (RecalcTotal actualiza currentRow.Cantidad en memoria);
    // sin suprimir el eco, cada edición de una medición recargaba toda la grilla de la
    // partida y mostraba el parpadeo de "Cargando partida…".
    private void MarkDirtySuppressed()
    {
        using (owner?.SuppressSessionReload() ?? NullDisposable)
            session.MarkDirty("cubicacion");
    }

    private static readonly IDisposable NullDisposable = new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private void RecalcTotal()
    {
        Total = Lines.Sum(l => l.Parcial);
        OnPropertyChanged(nameof(TotalDisplay));

        if (currentRow != null && Lines.Count > 0)
        {
            currentRow.Cantidad = Total;
            session.ApplyCubicacionMedicionesForPartida(currentRow.Source.ItemId);
        }
    }
}
