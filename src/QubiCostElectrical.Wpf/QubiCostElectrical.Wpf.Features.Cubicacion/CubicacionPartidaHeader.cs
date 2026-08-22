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

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public class CubicacionPartidaHeader : UserControl, IComponentConnector
{
	internal Border ItemRowCodigoCell;

	internal Border ItemRowDescCell;

	internal Border ItemRowUiCell;

	internal TextBox UnidadHeaderBox;

	internal Border ItemRowCantCell;

	internal TextBox CantHeaderBox;

	internal Border ItemRowMatCell;

	internal Border ItemRowMoCell;

	internal Border ItemRowTotalCell;

	internal Border ItemRowSubtotalCell;

	internal Border ItemRowGgUtCell;

	internal Border ItemRowUfUnitCell;

	internal Border ItemRowSubtotalUfCell;

	internal TextBox ObservacionHeaderBox;

	private bool _contentLoaded;

	public TextBox Unidad => UnidadHeaderBox;

	public TextBox Cantidad => CantHeaderBox;

	public TextBox Observaciones => ObservacionHeaderBox;

	public Border CodigoCell => ItemRowCodigoCell;

	public Border DescripcionCell => ItemRowDescCell;

	public Border UnidadCell => ItemRowUiCell;

	public Border CantidadCell => ItemRowCantCell;

	public Border MaterialCell => ItemRowMatCell;

	public Border ManoObraCell => ItemRowMoCell;

	public Border TotalCell => ItemRowTotalCell;

	public Border SubtotalCell => ItemRowSubtotalCell;

	public Border GgUtCell => ItemRowGgUtCell;

	public Border UfUnitCell => ItemRowUfUnitCell;

	public Border SubtotalUfCell => ItemRowSubtotalUfCell;

	public event RoutedEventHandler? FieldGotFocus;

	public event KeyEventHandler? FieldPreviewKeyDown;

	public event RoutedEventHandler? CantGotFocus;

	public event RoutedEventHandler? CantLostFocus;

	public event KeyEventHandler? CantPreviewKeyDown;

	public CubicacionPartidaHeader()
	{
		InitializeComponent();
	}

	private void OnFieldGotFocus(object sender, RoutedEventArgs e)
	{
		FieldGotFocus?.Invoke(sender, e);
	}

	private void OnFieldPreviewKeyDown(object sender, KeyEventArgs e)
	{
		FieldPreviewKeyDown?.Invoke(sender, e);
	}

	private void OnCantGotFocus(object sender, RoutedEventArgs e)
	{
		CantGotFocus?.Invoke(sender, e);
	}

	private void OnCantLostFocus(object sender, RoutedEventArgs e)
	{
		CantLostFocus?.Invoke(sender, e);
	}

	private void OnCantPreviewKeyDown(object sender, KeyEventArgs e)
	{
		CantPreviewKeyDown?.Invoke(sender, e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacion/cubicacionpartidaheader.xaml", UriKind.Relative);
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
			ItemRowCodigoCell = (Border)target;
			break;
		case 2:
			ItemRowDescCell = (Border)target;
			break;
		case 3:
			ItemRowUiCell = (Border)target;
			break;
		case 4:
			UnidadHeaderBox = (TextBox)target;
			UnidadHeaderBox.GotFocus += OnFieldGotFocus;
			UnidadHeaderBox.PreviewKeyDown += OnFieldPreviewKeyDown;
			break;
		case 5:
			ItemRowCantCell = (Border)target;
			break;
		case 6:
			CantHeaderBox = (TextBox)target;
			CantHeaderBox.GotFocus += OnCantGotFocus;
			CantHeaderBox.LostFocus += OnCantLostFocus;
			CantHeaderBox.PreviewKeyDown += OnCantPreviewKeyDown;
			break;
		case 7:
			ItemRowMatCell = (Border)target;
			break;
		case 8:
			ItemRowMoCell = (Border)target;
			break;
		case 9:
			ItemRowTotalCell = (Border)target;
			break;
		case 10:
			ItemRowSubtotalCell = (Border)target;
			break;
		case 11:
			ItemRowGgUtCell = (Border)target;
			break;
		case 12:
			ItemRowUfUnitCell = (Border)target;
			break;
		case 13:
			ItemRowSubtotalUfCell = (Border)target;
			break;
		case 14:
			ObservacionHeaderBox = (TextBox)target;
			ObservacionHeaderBox.GotFocus += OnFieldGotFocus;
			ObservacionHeaderBox.PreviewKeyDown += OnFieldPreviewKeyDown;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
