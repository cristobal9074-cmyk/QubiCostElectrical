namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica movida a CubicacionSessionCoordinator (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que el constructor y Dispose() ya conocen, sin cambiar ninguna firma.
    partial void InitializeSessionHandlers() => sessionCoordinator.Initialize();

    public void Dispose()
    {
        sessionCoordinator.Dispose();
        totalsCalculator.Dispose();
    }

    internal bool HasPendingUndoForEdit
    {
        get => hasPendingUndoForEdit;
        set => hasPendingUndoForEdit = value;
    }

    internal int SuppressSessionReloadCount => suppressSessionReload;

    internal void NotifyIsReadOnlyChanged() => OnPropertyChanged(nameof(IsReadOnly));
}
