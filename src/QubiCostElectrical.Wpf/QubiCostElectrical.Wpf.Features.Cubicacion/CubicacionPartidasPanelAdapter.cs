#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class CubicacionPartidasPanelAdapter : ICubicacionPartidasPanelHost
{
	private readonly CubicacionViewModel viewModel;

	private readonly ListBox partidasList;

	public ObservableCollection<PartidaListItemVm> FilteredPartidas => viewModel.FilteredPartidas;

	public PartidaListItemVm? SelectedPartida
	{
		get
		{
			return viewModel.SelectedPartida;
		}
		set
		{
			viewModel.SelectedPartida = value;
		}
	}

	public string PartidaFilter
	{
		get
		{
			return viewModel.PartidaFilter;
		}
		set
		{
			viewModel.PartidaFilter = value;
		}
	}

	public CubicacionPartidasPanelAdapter(CubicacionViewModel viewModel, ListBox partidasList)
	{
		this.viewModel = viewModel;
		this.partidasList = partidasList;
	}

	public void ScrollToSelectedPartida()
	{
		if (SelectedPartida != null)
		{
			partidasList.ScrollIntoView(SelectedPartida);
		}
	}

	public void SelectPartida(PartidaListItemVm partida)
	{
		SelectedPartida = partida;
		partidasList.SelectedItem = partida;
		partidasList.ScrollIntoView(partida);
	}
}
