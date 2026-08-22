// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using QubiCostElectrical.Wpf.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public class CubicacionResourceGrid : UserControl, IComponentConnector
{
	public delegate void CurrentCellChangedForwardedHandler(object sender, EventArgs e);

	public delegate void BeginningEditForwardedHandler(object sender, DataGridBeginningEditEventArgs e);

	public delegate void PreparingCellForEditForwardedHandler(object sender, DataGridPreparingCellForEditEventArgs e);

	public delegate void CellEditEndingForwardedHandler(object sender, DataGridCellEditEndingEventArgs e);

	internal DataGrid ResourceGrid;

	internal DataGridTemplateColumn IndexColumn;

	internal DataGridTextColumn CodigoColumn;

	internal DataGridTextColumn DescripcionColumn;

	internal DataGridTextColumn UnidadColumn;

	internal DataGridTextColumn CantidadColumn;

	internal DataGridTextColumn MaterialPriceColumn;

	internal DataGridTextColumn LaborPriceColumn;

	internal DataGridTextColumn TotalPriceColumn;

	internal DataGridTextColumn SubtotalColumn;

	internal DataGridTextColumn GgUtColumn;

	internal DataGridTextColumn UfUnitColumn;

	internal DataGridTextColumn SubtotalUfColumn;

	internal DisplayBindingTextColumn ObservacionesColumn;

	private bool _contentLoaded;

	public DataGrid Grid => ResourceGrid;

	public DataGridColumn Index => IndexColumn;

	public DataGridColumn Codigo => CodigoColumn;

	public DataGridColumn Descripcion => DescripcionColumn;

	public DataGridColumn Unidad => UnidadColumn;

	public DataGridTextColumn Cantidad => CantidadColumn;

	public DataGridColumn MaterialPrice => MaterialPriceColumn;

	public DataGridColumn LaborPrice => LaborPriceColumn;

	public DataGridColumn TotalPrice => TotalPriceColumn;

	public DataGridColumn Subtotal => SubtotalColumn;

	public DataGridColumn GgUt => GgUtColumn;

	public DataGridColumn UfUnit => UfUnitColumn;

	public DataGridColumn SubtotalUf => SubtotalUfColumn;

	public DataGridColumn Observaciones => ObservacionesColumn;

	public event BeginningEditForwardedHandler? BeginningEditForwarded;

	public event PreparingCellForEditForwardedHandler? PreparingCellForEditForwarded;

	public event CellEditEndingForwardedHandler? CellEditEndingForwarded;

	public event KeyEventHandler? PreviewKeyDownForwarded;

	public event MouseButtonEventHandler? PreviewMouseLeftButtonDownForwarded;

	public event ContextMenuEventHandler? ContextMenuOpeningForwarded;

	public event SelectionChangedEventHandler? SelectionChangedForwarded;

	public event CurrentCellChangedForwardedHandler? CurrentCellChangedForwarded;

	public CubicacionResourceGrid()
	{
		InitializeComponent();
	}

	private void OnBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
	{
		BeginningEditForwarded?.Invoke(sender, e);
	}

	private void OnPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
	{
		PreparingCellForEditForwarded?.Invoke(sender, e);
	}

	private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
	{
		CellEditEndingForwarded?.Invoke(sender, e);
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		PreviewKeyDownForwarded?.Invoke(sender, e);
	}

	private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		PreviewMouseLeftButtonDownForwarded?.Invoke(sender, e);
	}

	private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		ContextMenuOpeningForwarded?.Invoke(sender, e);
	}

	private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		SelectionChangedForwarded?.Invoke(sender, e);
	}

	private void OnCurrentCellChanged(object sender, EventArgs e)
	{
		CurrentCellChangedForwarded?.Invoke(sender, e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacion/cubicacionresourcegrid.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			ResourceGrid = (DataGrid)target;
			ResourceGrid.BeginningEdit += OnBeginningEdit;
			ResourceGrid.PreparingCellForEdit += OnPreparingCellForEdit;
			ResourceGrid.CellEditEnding += OnCellEditEnding;
			ResourceGrid.PreviewKeyDown += OnPreviewKeyDown;
			ResourceGrid.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
			ResourceGrid.ContextMenuOpening += OnContextMenuOpening;
			ResourceGrid.SelectionChanged += OnSelectionChanged;
			ResourceGrid.CurrentCellChanged += OnCurrentCellChanged;
			break;
		case 2:
			IndexColumn = (DataGridTemplateColumn)target;
			break;
		case 3:
			CodigoColumn = (DataGridTextColumn)target;
			break;
		case 4:
			DescripcionColumn = (DataGridTextColumn)target;
			break;
		case 5:
			UnidadColumn = (DataGridTextColumn)target;
			break;
		case 6:
			CantidadColumn = (DataGridTextColumn)target;
			break;
		case 7:
			MaterialPriceColumn = (DataGridTextColumn)target;
			break;
		case 8:
			LaborPriceColumn = (DataGridTextColumn)target;
			break;
		case 9:
			TotalPriceColumn = (DataGridTextColumn)target;
			break;
		case 10:
			SubtotalColumn = (DataGridTextColumn)target;
			break;
		case 11:
			GgUtColumn = (DataGridTextColumn)target;
			break;
		case 12:
			UfUnitColumn = (DataGridTextColumn)target;
			break;
		case 13:
			SubtotalUfColumn = (DataGridTextColumn)target;
			break;
		case 14:
			ObservacionesColumn = (DisplayBindingTextColumn)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
