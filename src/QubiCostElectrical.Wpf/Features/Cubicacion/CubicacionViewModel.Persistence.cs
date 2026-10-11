using System.Collections.Generic;
using System.Threading.Tasks;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica movida a CubicacionPersistenceService (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que el resto de los partials, RightPanelVm y CubicacionView.xaml.cs
    // ya conocen, sin cambiar ninguna firma.
    public Task SaveAsync() => persistenceService.SaveAsync();

    internal void MarkDirty() => persistenceService.MarkDirty();

    internal void MarkClean() => persistenceService.MarkClean();

    internal void NotifyCubicacionSummaryChanged()
    {
        OnPropertyChanged(nameof(PartidaEstadoText));
        OnPropertyChanged(nameof(PartidaResumenText));
    }

    internal void NotifyIsEmptyChanged() => OnPropertyChanged(nameof(IsEmpty));

    internal IReadOnlyDictionary<string, MaterialRow>? ValidationMaterialesIndex
    {
        get => validationMaterialesIndex;
        set => validationMaterialesIndex = value;
    }

    internal void UpdatePartidaCount(PartidaListItemVm? partida) => persistenceService.UpdatePartidaCount(partida);

    public void ValidateRows() => persistenceService.ValidateRows();

    internal void ValidateRow(CubicacionRowVm row) => persistenceService.ValidateRow(row);

    internal IReadOnlyDictionary<string, MaterialRow> GetValidationMaterialesIndex() => persistenceService.GetValidationMaterialesIndex();

    public void OnRowEdited(CubicacionRowVm row, ProjectFileData? preEditSnapshot = null) => persistenceService.OnRowEdited(row, preEditSnapshot);

    private void AplicarConjunto() => persistenceService.AplicarConjunto();

    internal void SyncGridRowSelection(IEnumerable<CubicacionRowVm> selected) => persistenceService.SyncGridRowSelection(selected);
}
