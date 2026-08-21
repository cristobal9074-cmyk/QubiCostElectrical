namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica movida a CubicacionLoadingService (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que el constructor, CubicacionView.xaml.cs y el resto de los partials
    // ya conocen, sin cambiar ninguna firma.
    internal void LoadPartidas() => loadingService.LoadPartidas();

    private void ApplyFilter() => loadingService.ApplyFilter();

    internal void SelectFirstPartida() => loadingService.SelectFirstPartida();

    public void SelectPartida(PartidaListItemVm partida) => loadingService.SelectPartida(partida);

    internal void LoadCubicacionRows() => loadingService.LoadCubicacionRows();
}
