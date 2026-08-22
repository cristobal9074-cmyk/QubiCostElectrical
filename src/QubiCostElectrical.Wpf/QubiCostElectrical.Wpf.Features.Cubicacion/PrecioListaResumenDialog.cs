// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public class PrecioListaResumenDialog : Window, IComponentConnector, IStyleConnector
{
	private readonly PrecioListaResumenVm vm;

	private TextBox? _activeEditor;

	private bool _isEditingCodigo;

	internal DataGrid MainGrid;

	internal Popup AutocompletePopup;

	internal ListBox AutocompleteList;

	private bool _contentLoaded;

	public PrecioListaResumenDialog(BudgetProjectSession session, IPriceBankStore priceBankStore, string autoFilterCodigo = "")
	{
		InitializeComponent();
		vm = new PrecioListaResumenVm(session, priceBankStore, autoFilterCodigo);
		base.DataContext = vm;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (MainGrid.Items.Count > 0)
		{
			MainGrid.Focus();
			if (MainGrid.Columns.Count > 3)
			{
				MainGrid.CurrentCell = new DataGridCellInfo(MainGrid.Items[0], MainGrid.Columns[3]);
				MainGrid.BeginEdit();
			}
		}
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			if (vm.AutocompleteVisible)
			{
				vm.AutocompleteVisible = false;
				e.Handled = true;
			}
			else
			{
				Close();
				e.Handled = true;
			}
		}
	}

	private void OnGridPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
	{
		if (_activeEditor != null)
		{
			_activeEditor.TextChanged -= OnEditorTextChanged;
			_activeEditor.PreviewKeyDown -= OnEditorPreviewKeyDown;
			_activeEditor.LostFocus -= OnEditorLostFocus;
		}
		if (e.EditingElement is TextBox textBox)
		{
			string text = e.Column.Header as string;
			if (!(text != "CÃ“DIGO") || !(text != "DESCRIPCIÃ“N"))
			{
				_activeEditor = textBox;
				_isEditingCodigo = text == "CÃ“DIGO";
				textBox.TextChanged += OnEditorTextChanged;
				textBox.PreviewKeyDown += OnEditorPreviewKeyDown;
				AutocompletePopup.PlacementTarget = textBox;
				textBox.LostFocus += OnEditorLostFocus;
			}
		}
	}

	private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
	{
		if (_activeEditor != null)
		{
			string text = _activeEditor.Text;
			vm.SearchBank(text);
		}
	}

	private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs e)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Invalid comparison between Unknown and I4
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		if (!vm.AutocompleteVisible)
		{
			return;
		}
		Key key = e.Key;
		if ((int)key <= 13)
		{
			if ((int)key != 6)
			{
				if ((int)key == 13)
				{
					vm.AutocompleteVisible = false;
					e.Handled = true;
				}
			}
			else
			{
				if (AutocompleteList.SelectedItem is PriceBankSuggestionVm suggestion)
				{
					ApplySuggestion(suggestion);
				}
				e.Handled = true;
			}
		}
		else if ((int)key != 24)
		{
			if ((int)key == 26)
			{
				AutocompleteList.SelectedIndex = Math.Min(AutocompleteList.SelectedIndex + 1, AutocompleteList.Items.Count - 1);
				if (AutocompleteList.SelectedIndex < 0)
				{
					AutocompleteList.SelectedIndex = 0;
				}
				e.Handled = true;
			}
		}
		else
		{
			AutocompleteList.SelectedIndex = Math.Max(AutocompleteList.SelectedIndex - 1, 0);
			e.Handled = true;
		}
	}

	private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
	{
		if (!AutocompletePopup.IsMouseOver)
		{
			vm.AutocompleteVisible = false;
		}
	}

	private void OnAutocompleteMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (AutocompleteList.SelectedItem is PriceBankSuggestionVm suggestion)
		{
			ApplySuggestion(suggestion);
		}
	}

	private void ApplySuggestion(PriceBankSuggestionVm suggestion)
	{
		if (MainGrid.CurrentItem is PrecioListaMaterialVm target)
		{
			vm.ApplySuggestion(target, suggestion);
			vm.AutocompleteVisible = false;
			MainGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
		}
	}

	private void OnAgregarClick(object sender, RoutedEventArgs e)
	{
		PrecioListaEditarDialog precioListaEditarDialog = new PrecioListaEditarDialog(vm, null);
		precioListaEditarDialog.Owner = this;
		precioListaEditarDialog.ShowDialog();
	}

	private void OnEliminarClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: PrecioListaMaterialVm tag } && WpfDialogService.Confirm("Eliminar material", $"Â¿Eliminar '{tag.Codigo}' Â· {tag.Descripcion} de la lista de precios?"))
		{
			vm.EliminarMaterial(tag);
		}
	}

	private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
	{
		if (e.EditAction == DataGridEditAction.Cancel || !(e.Row.Item is PrecioListaMaterialVm precioListaMaterialVm) || !(e.Column is DataGridTextColumn dataGridTextColumn) || !(e.EditingElement is TextBox textBox))
		{
			return;
		}
		string text = dataGridTextColumn.Header as string;
		if (text == "CÃ“DIGO" || text == "DESCRIPCIÃ“N")
		{
			string text2 = textBox.Text.Trim();
			if (string.IsNullOrWhiteSpace(text2))
			{
				return;
			}
			if (text == "CÃ“DIGO")
			{
				precioListaMaterialVm.Codigo = text2;
				PriceBankEntry priceBankEntry = vm.LookupByCodigo(text2);
				if (priceBankEntry != null)
				{
					precioListaMaterialVm.Descripcion = priceBankEntry.Descripcion ?? "";
					precioListaMaterialVm.Unidad = priceBankEntry.Unidad ?? "";
					precioListaMaterialVm.MatClp = priceBankEntry.MatClp;
					precioListaMaterialVm.MoClp = priceBankEntry.MoClp;
				}
			}
			else
			{
				precioListaMaterialVm.Descripcion = text2;
				(decimal, decimal)? tuple = vm.LookupPricesByDescripcion(text2);
				if (tuple.HasValue)
				{
					precioListaMaterialVm.MatClp = tuple.Value.Item1;
					precioListaMaterialVm.MoClp = tuple.Value.Item2;
				}
			}
			precioListaMaterialVm.FechaPrecio = DateTime.Today;
			vm.ActualizarPrecioEnSesion(precioListaMaterialVm);
		}
		else
		{
			if (text != "MAT $ CLP" && text != "MO $ CLP")
			{
				return;
			}
			string text3 = textBox.Text.Replace(".", "").Replace(",", "").Trim();
			if (!string.IsNullOrWhiteSpace(text3) && decimal.TryParse(text3, out var result) && !(result < 0m))
			{
				if (text == "MAT $ CLP")
				{
					precioListaMaterialVm.MatClp = result;
				}
				else
				{
					precioListaMaterialVm.MoClp = result;
				}
				precioListaMaterialVm.FechaPrecio = DateTime.Today;
				vm.ActualizarPrecioEnSesion(precioListaMaterialVm);
			}
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacion/preciolistaresumendialog.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((PrecioListaResumenDialog)target).PreviewKeyDown += OnPreviewKeyDown;
			((PrecioListaResumenDialog)target).Loaded += OnLoaded;
			break;
		case 2:
			((Button)target).Click += OnAgregarClick;
			break;
		case 3:
			MainGrid = (DataGrid)target;
			MainGrid.PreparingCellForEdit += OnGridPreparingCellForEdit;
			MainGrid.CellEditEnding += OnCellEditEnding;
			break;
		case 5:
			AutocompletePopup = (Popup)target;
			break;
		case 6:
			AutocompleteList = (ListBox)target;
			AutocompleteList.PreviewMouseLeftButtonDown += OnAutocompleteMouseDown;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 4)
		{
			((Button)target).Click += OnEliminarClick;
		}
	}
}
