#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ResumenItemizadoViewModel : ViewModelBase
{
	private readonly List<ItemizadoPartidaVm> allItems = new List<ItemizadoPartidaVm>();

	private string searchText = "";

	private string activeFilter = "Todas";

	private bool showAll = true;

	private bool onlyCubicacion;

	private bool onlySinCubicar;

	private bool onlyTitulos;

	private ItemizadoPartidaVm? selectedRow;

	public string? InitialId { get; }

	public ObservableCollection<FilterChipVm> Filters { get; }

	public ObservableCollection<ItemizadoPartidaVm> FlatRows { get; } = new ObservableCollection<ItemizadoPartidaVm>();

	public string ActiveFilter
	{
		get
		{
			return activeFilter;
		}
		set
		{
			if (!SetProperty(ref activeFilter, value, "ActiveFilter"))
			{
				return;
			}
			showAll = string.Equals(value, "Todas", StringComparison.OrdinalIgnoreCase);
			onlyCubicacion = string.Equals(value, "Con cubicación", StringComparison.OrdinalIgnoreCase);
			onlySinCubicar = string.Equals(value, "Sin cubicar", StringComparison.OrdinalIgnoreCase);
			onlyTitulos = string.Equals(value, "Títulos", StringComparison.OrdinalIgnoreCase);
			foreach (FilterChipVm filter in Filters)
			{
				filter.IsActive = string.Equals(filter.Nombre, value, StringComparison.OrdinalIgnoreCase);
			}
			AplicarFiltros();
		}
	}

	public string SearchText
	{
		get
		{
			return searchText;
		}
		set
		{
			if (SetProperty(ref searchText, value, "SearchText"))
			{
				AplicarFiltros();
			}
		}
	}

	public ItemizadoPartidaVm? SelectedRow
	{
		get
		{
			return selectedRow;
		}
		set
		{
			if (SetProperty(ref selectedRow, value, "SelectedRow"))
			{
				CommandManager.InvalidateRequerySuggested();
			}
		}
	}

	public ICommand CerrarCommand { get; }

	public Action? OnCerrar { get; set; }

	public string SummaryText
	{
		get
		{
			int value = allItems.Count((ItemizadoPartidaVm i) => !i.EsSeccion);
			int value2 = allItems.Count((ItemizadoPartidaVm i) => !i.EsSeccion && i.TieneCubicacion);
			decimal num = allItems.Sum((ItemizadoPartidaVm i) => i.MaterialTotal);
			decimal num2 = allItems.Sum((ItemizadoPartidaVm i) => i.ManoObraTotal);
			return $"{value} partidas · {value2} con cubicación · Total: $ {num + num2:N0}";
		}
	}

	public ResumenItemizadoViewModel(IReadOnlyList<ItemizadoPartidaVm> items, string? initialId = null)
	{
		allItems.AddRange(items);
		InitialId = initialId;
		Filters = new ObservableCollection<FilterChipVm>
		{
			new FilterChipVm
			{
				Nombre = "Todas",
				IsActive = true,
				Count = items.Count
			},
			new FilterChipVm
			{
				Nombre = "Con cubicación",
				IsActive = false,
				Count = items.Count((ItemizadoPartidaVm i) => !i.EsSeccion && i.TieneCubicacion)
			},
			new FilterChipVm
			{
				Nombre = "Sin cubicar",
				IsActive = false,
				Count = items.Count((ItemizadoPartidaVm i) => !i.EsSeccion && !i.TieneCubicacion)
			},
			new FilterChipVm
			{
				Nombre = "Títulos",
				IsActive = false,
				Count = items.Count((ItemizadoPartidaVm i) => i.EsSeccion)
			}
		};
		CerrarCommand = new RelayCommand(Cerrar);
		AplicarFiltros();
	}

	private void AplicarFiltros()
	{
		FlatRows.Clear();
		string text = SearchText?.Trim() ?? "";
		bool flag = text.Length > 0;
		foreach (ItemizadoPartidaVm allItem in allItems)
		{
			if (PassesFilter(allItem) && (!flag || MatchesSearch(allItem, text)))
			{
				FlatRows.Add(allItem);
			}
		}
		if (InitialId != null)
		{
			SelectedRow = FlatRows.FirstOrDefault((ItemizadoPartidaVm r) => string.Equals(r.Id, InitialId, StringComparison.OrdinalIgnoreCase));
		}
	}

	private bool PassesFilter(ItemizadoPartidaVm item)
	{
		if (showAll)
		{
			return true;
		}
		if (onlyTitulos)
		{
			return item.EsSeccion;
		}
		if (item.EsSeccion)
		{
			return false;
		}
		if (onlyCubicacion)
		{
			return item.TieneCubicacion;
		}
		if (onlySinCubicar)
		{
			return !item.TieneCubicacion;
		}
		return true;
	}

	private static bool MatchesSearch(ItemizadoPartidaVm item, string search)
	{
		if (item.Descripcion.Contains(search, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (item.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return false;
	}

	private void Cerrar()
	{
		OnCerrar?.Invoke();
	}
}
