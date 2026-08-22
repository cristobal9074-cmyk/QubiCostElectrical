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

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public class CubicacionScGrid : UserControl, IComponentConnector, IStyleConnector
{
	public delegate void GridEventHandler(object sender, EventArgs e);

	internal DataGrid CircuitosGrid;

	private bool _contentLoaded;

	public event EventHandler? CurrentCellChanged;

	public new event MouseButtonEventHandler? PreviewMouseLeftButtonDown;

	public new event KeyEventHandler? PreviewKeyDown;

	public new event TextCompositionEventHandler? PreviewTextInput;

	public event RoutedEventHandler? TipoEditorLostFocus;

	public event RoutedEventHandler? FaseEditorLostFocus;

	public event RoutedEventHandler? PisosBtnClick;

	public event GridEventHandler? PisosPopupOpened;

	public event GridEventHandler? PisosPopupClosed;

	public event KeyboardFocusChangedEventHandler? PisosFlyoutLostKeyboardFocus;

	public event KeyEventHandler? PisosPopupInputPreviewKeyDown;

	public event RoutedEventHandler? PisosPopupInputGotFocus;

	public event RoutedEventHandler? FilterableComboLoaded;

	public event TextChangedEventHandler? FilterableComboTextChanged;

	public event RoutedEventHandler? ConductorBandEditorLostFocus;

	public event RoutedEventHandler? SeccionBandEditorLostFocus;

	public event RoutedEventHandler? TipoTuberiaEditorLostFocus;

	public event RoutedEventHandler? SeccionTuberiaEditorLostFocus;

	public event RoutedEventHandler? ConductorPvcEditorLostFocus;

	public event RoutedEventHandler? SeccionPvcEditorLostFocus;

	public CubicacionScGrid()
	{
		InitializeComponent();
		if (base.Resources.MergedDictionaries.Count > 0 && base.Resources.MergedDictionaries[0] is CubicacionScGridColumns cubicacionScGridColumns)
		{
			cubicacionScGridColumns.TipoEditorLostFocus += delegate(object s, RoutedEventArgs e)
			{
				TipoEditorLostFocus?.Invoke(s, e);
			};
			cubicacionScGridColumns.FaseEditorLostFocus += delegate(object s, RoutedEventArgs e)
			{
				FaseEditorLostFocus?.Invoke(s, e);
			};
			cubicacionScGridColumns.PisosBtnClick += delegate(object s, RoutedEventArgs e)
			{
				PisosBtnClick?.Invoke(s, e);
			};
			cubicacionScGridColumns.PisosPopupOpened += delegate(object? s, EventArgs e)
			{
				PisosPopupOpened?.Invoke(s ?? this, e);
			};
			cubicacionScGridColumns.PisosPopupClosed += delegate(object? s, EventArgs e)
			{
				PisosPopupClosed?.Invoke(s ?? this, e);
			};
			cubicacionScGridColumns.PisosFlyoutLostKeyboardFocus += delegate(object s, KeyboardFocusChangedEventArgs e)
			{
				PisosFlyoutLostKeyboardFocus?.Invoke(s, e);
			};
			cubicacionScGridColumns.PisosPopupInputPreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				PisosPopupInputPreviewKeyDown?.Invoke(s, e);
			};
			cubicacionScGridColumns.PisosPopupInputGotFocus += delegate(object s, RoutedEventArgs e)
			{
				PisosPopupInputGotFocus?.Invoke(s, e);
			};
		}
	}

	private void OnCircuitosGrid_CurrentCellChanged(object? s, EventArgs e)
	{
		CurrentCellChanged?.Invoke(s, e);
	}

	private void OnCircuitosGrid_PreviewMouseLeftButtonDown(object s, MouseButtonEventArgs e)
	{
		PreviewMouseLeftButtonDown?.Invoke(s, e);
	}

	private void OnCircuitosGrid_PreviewKeyDown(object s, KeyEventArgs e)
	{
		PreviewKeyDown?.Invoke(s, e);
	}

	private void OnCircuitosGrid_PreviewTextInput(object s, TextCompositionEventArgs e)
	{
		PreviewTextInput?.Invoke(s, e);
	}

	private void OnTipoEditor_LostFocus(object s, RoutedEventArgs e)
	{
		TipoEditorLostFocus?.Invoke(s, e);
	}

	private void OnFaseEditor_LostFocus(object s, RoutedEventArgs e)
	{
		FaseEditorLostFocus?.Invoke(s, e);
	}

	private void OnPisosBtn_Click(object s, RoutedEventArgs e)
	{
		PisosBtnClick?.Invoke(s, e);
	}

	private void OnPisosPopup_Opened(object s, EventArgs e)
	{
		PisosPopupOpened?.Invoke(s, e);
	}

	private void OnPisosPopup_Closed(object s, EventArgs e)
	{
		PisosPopupClosed?.Invoke(s, e);
	}

	private void OnPisosFlyout_LostKeyboardFocus(object s, KeyboardFocusChangedEventArgs e)
	{
		PisosFlyoutLostKeyboardFocus?.Invoke(s, e);
	}

	private void OnPisosPopupInput_PreviewKeyDown(object s, KeyEventArgs e)
	{
		PisosPopupInputPreviewKeyDown?.Invoke(s, e);
	}

	private void OnPisosPopupInput_GotFocus(object s, RoutedEventArgs e)
	{
		PisosPopupInputGotFocus?.Invoke(s, e);
	}

	private void OnFilterableCombo_Loaded(object s, RoutedEventArgs e)
	{
		FilterableComboLoaded?.Invoke(s, e);
	}

	private void OnFilterableCombo_TextChanged(object s, TextChangedEventArgs e)
	{
		FilterableComboTextChanged?.Invoke(s, e);
	}

	private void OnConductorBandEditor_LostFocus(object s, RoutedEventArgs e)
	{
		ConductorBandEditorLostFocus?.Invoke(s, e);
	}

	private void OnSeccionBandEditor_LostFocus(object s, RoutedEventArgs e)
	{
		SeccionBandEditorLostFocus?.Invoke(s, e);
	}

	private void OnTipoTuberiaEditor_LostFocus(object s, RoutedEventArgs e)
	{
		TipoTuberiaEditorLostFocus?.Invoke(s, e);
	}

	private void OnSeccionTuberiaEditor_LostFocus(object s, RoutedEventArgs e)
	{
		SeccionTuberiaEditorLostFocus?.Invoke(s, e);
	}

	private void OnConductorPvcEditor_LostFocus(object s, RoutedEventArgs e)
	{
		ConductorPvcEditorLostFocus?.Invoke(s, e);
	}

	private void OnSeccionPvcEditor_LostFocus(object s, RoutedEventArgs e)
	{
		SeccionPvcEditorLostFocus?.Invoke(s, e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacionsc/cubicacionscgrid.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 1)
		{
			CircuitosGrid = (DataGrid)target;
			CircuitosGrid.CurrentCellChanged += OnCircuitosGrid_CurrentCellChanged;
			CircuitosGrid.PreviewMouseLeftButtonDown += OnCircuitosGrid_PreviewMouseLeftButtonDown;
			CircuitosGrid.PreviewKeyDown += OnCircuitosGrid_PreviewKeyDown;
			CircuitosGrid.PreviewTextInput += OnCircuitosGrid_PreviewTextInput;
		}
		else
		{
			_contentLoaded = true;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 2:
			((ComboBox)target).Loaded += OnFilterableCombo_Loaded;
			((ComboBox)target).AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnFilterableCombo_TextChanged));
			((ComboBox)target).LostFocus += OnConductorBandEditor_LostFocus;
			break;
		case 3:
			((ComboBox)target).Loaded += OnFilterableCombo_Loaded;
			((ComboBox)target).AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnFilterableCombo_TextChanged));
			((ComboBox)target).LostFocus += OnSeccionBandEditor_LostFocus;
			break;
		case 4:
			((ComboBox)target).Loaded += OnFilterableCombo_Loaded;
			((ComboBox)target).AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnFilterableCombo_TextChanged));
			((ComboBox)target).LostFocus += OnTipoTuberiaEditor_LostFocus;
			break;
		case 5:
			((ComboBox)target).Loaded += OnFilterableCombo_Loaded;
			((ComboBox)target).AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnFilterableCombo_TextChanged));
			((ComboBox)target).LostFocus += OnSeccionTuberiaEditor_LostFocus;
			break;
		case 6:
			((ComboBox)target).Loaded += OnFilterableCombo_Loaded;
			((ComboBox)target).AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnFilterableCombo_TextChanged));
			((ComboBox)target).LostFocus += OnConductorPvcEditor_LostFocus;
			break;
		case 7:
			((ComboBox)target).Loaded += OnFilterableCombo_Loaded;
			((ComboBox)target).AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnFilterableCombo_TextChanged));
			((ComboBox)target).LostFocus += OnSeccionPvcEditor_LostFocus;
			break;
		}
	}
}
