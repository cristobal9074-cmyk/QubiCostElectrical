#nullable enable
using System.Collections.ObjectModel;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public interface ICubicacionPartidasPanelHost
{
	ObservableCollection<PartidaListItemVm> FilteredPartidas { get; }

	PartidaListItemVm? SelectedPartida { get; set; }

	string PartidaFilter { get; set; }

	void ScrollToSelectedPartida();

	void SelectPartida(PartidaListItemVm partida);
}
